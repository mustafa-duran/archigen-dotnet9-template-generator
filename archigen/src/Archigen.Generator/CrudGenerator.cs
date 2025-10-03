using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Archigen.Core;

namespace Archigen.Generator;

public sealed class CrudGenerator
{
    public void Generate(ProjectLayout layout, CrudGenerationOptions options)
    {
        EntityDefinition entity = EnsureDomainEntity(layout, options);

        GeneratePersistenceArtifacts(layout, entity, options);
        GenerateApplicationArtifacts(layout, entity, options);
        GenerateServices(layout, entity);
        GenerateWebApiController(layout, entity);

        // Update OperationClaimConfiguration if security is enabled
        if (options.EnableSecurity)
        {
            UpdateOperationClaimConfiguration(layout, entity);
        }
    }

    private EntityDefinition EnsureDomainEntity(ProjectLayout layout, CrudGenerationOptions options)
    {
        string entitiesDir = Path.Combine(layout.DomainPath, "Entities");
        Directory.CreateDirectory(entitiesDir);

        string entityPath = Path.Combine(entitiesDir, $"{options.EntityName.ToPascalCase()}.cs");
        if (File.Exists(entityPath))
            return EntityParser.Parse(entityPath);

        if (options.Properties.Count == 0)
            throw new InvalidOperationException("Entity does not exist yet. Provide --props to create a new domain entity.");

        EntityDefinition definition = new()
        {
            Name = options.EntityName.ToPascalCase(),
            IdType = options.IdType,
            Properties = options.Properties.Select(p => new PropertyDefinition { Name = p.Name, Type = p.Type }).ToList()
        };

        WriteDomainEntity(layout, definition, entityPath);
        return definition;
    }

    private static void WriteDomainEntity(ProjectLayout layout, EntityDefinition entity, string entityPath)
    {
        StringBuilder sb = new();
        sb.AppendLine("using Core.Persistence.Repositories;");
        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ProjectName}.Domain.Entities;");
        sb.AppendLine();
        sb.AppendLine($"public class {entity.NamePascal} : Entity<{entity.IdType}>");
        sb.AppendLine("{");

        // Properties
        foreach (PropertyDefinition property in entity.Properties)
        {
            sb.AppendLine($"    public {property.Type} {property.PropertyName} {{ get; set; }}");
        }

        sb.AppendLine();

        // Default constructor - initialize non-nullable string properties with empty strings
        sb.AppendLine($"    public {entity.NamePascal}()");
        sb.AppendLine("    {");
        foreach (PropertyDefinition property in entity.Properties)
        {
            // Initialize all string properties with string.Empty for safety (nullable or not)
            if (property.NonNullableType.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"        {property.PropertyName} = string.Empty;");
            }
        }
        sb.AppendLine("    }");

        // Parameterized constructor
        if (entity.Properties.Count > 0)
        {
            sb.AppendLine();
            var parameters = entity.Properties
                .Select(p => $"{p.Type} {p.PropertyName.ToCamelCase()}")
                .ToArray();

            sb.AppendLine($"    public {entity.NamePascal}({string.Join(", ", parameters)})");
            sb.AppendLine("    {");

            foreach (PropertyDefinition property in entity.Properties)
            {
                sb.AppendLine($"        {property.PropertyName} = {property.PropertyName.ToCamelCase()};");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        File.WriteAllText(entityPath, sb.ToString());
    }

    private void GeneratePersistenceArtifacts(ProjectLayout layout, EntityDefinition entity, CrudGenerationOptions options)
    {
        GenerateEntityConfiguration(layout, entity);
        GenerateRepositoryInterface(layout, entity);
        GenerateRepositoryImplementation(layout, entity, options.DbContextName);
        RegisterRepository(layout, entity);
        UpdateDbContext(layout, entity, options.DbContextName);
    }

    private void GenerateEntityConfiguration(ProjectLayout layout, EntityDefinition entity)
    {
        string dir = Path.Combine(layout.PersistencePath, "EntityConfigurations");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, $"{entity.NamePascal}Configuration.cs");
        if (File.Exists(filePath))
            return;

        StringBuilder sb = new();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore.Metadata.Builders;");
        sb.AppendLine();
        sb.AppendLine($"using {layout.ProjectName}.Domain.Entities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ProjectName}.Persistence.EntityConfigurations;");
        sb.AppendLine();
        sb.AppendLine($"public class {entity.NamePascal}Configuration : IEntityTypeConfiguration<{entity.NamePascal}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public void Configure(EntityTypeBuilder<{entity.NamePascal}> builder)");
        sb.AppendLine("    {");

        // Table configuration with primary key
        sb.AppendLine($"        builder.ToTable(\"{entity.PluralPascal}\").HasKey({entity.NamePascal.ToLowerInvariant()} => {entity.NamePascal.ToLowerInvariant()}.Id);");
        sb.AppendLine();

        // Id property configuration
        sb.AppendLine($"        builder.Property({entity.NamePascal.ToLowerInvariant()} => {entity.NamePascal.ToLowerInvariant()}.Id).HasColumnName(\"Id\").IsRequired();");

        // Custom properties configuration
        foreach (PropertyDefinition property in entity.Properties)
        {
            string isRequired = property.IsNullable ? "" : ".IsRequired()";
            sb.AppendLine($"        builder.Property({entity.NamePascal.ToLowerInvariant()} => {entity.NamePascal.ToLowerInvariant()}.{property.PropertyName}).HasColumnName(\"{property.PropertyName}\"){isRequired};");
        }

        // Base Entity properties (CreatedDate, UpdatedDate, DeletedDate)
        sb.AppendLine($"        builder.Property({entity.NamePascal.ToLowerInvariant()} => {entity.NamePascal.ToLowerInvariant()}.CreatedDate).HasColumnName(\"CreatedDate\").IsRequired();");
        sb.AppendLine($"        builder.Property({entity.NamePascal.ToLowerInvariant()} => {entity.NamePascal.ToLowerInvariant()}.UpdatedDate).HasColumnName(\"UpdatedDate\");");
        sb.AppendLine($"        builder.Property({entity.NamePascal.ToLowerInvariant()} => {entity.NamePascal.ToLowerInvariant()}.DeletedDate).HasColumnName(\"DeletedDate\");");
        sb.AppendLine();

        // Soft delete query filter
        sb.AppendLine($"        builder.HasQueryFilter({entity.NamePascal.ToLowerInvariant()} => !{entity.NamePascal.ToLowerInvariant()}.DeletedDate.HasValue);");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        File.WriteAllText(filePath, sb.ToString());
    }

    private void GenerateRepositoryInterface(ProjectLayout layout, EntityDefinition entity)
    {
        string dir = Path.Combine(layout.ApplicationPath, "Services", "Repositories");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, $"I{entity.NamePascal}Repository.cs");

        StringBuilder sb = new();
        sb.AppendLine("using Core.Persistence.Repositories;");
        sb.AppendLine($"using {layout.ProjectName}.Domain.Entities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ProjectName}.Application.Services.Repositories;");
        sb.AppendLine();
        sb.AppendLine($"public interface I{entity.NamePascal}Repository : IAsyncRepository<{entity.NamePascal}, {entity.IdType}>, IRepository<{entity.NamePascal}, {entity.IdType}> {{ }}");

        WriteFileIfNeeded(filePath, sb.ToString(), existing =>
            existing.Contains("global::", StringComparison.Ordinal)
            || existing.Contains($"IAsyncRepository<{layout.ProjectName}.Domain.Entities.{entity.NamePascal}", StringComparison.Ordinal)
            || existing.Contains($"IRepository<{layout.ProjectName}.Domain.Entities.{entity.NamePascal}", StringComparison.Ordinal)
            || existing.Contains($"IAsyncRepository<{entity.NamePascal},", StringComparison.Ordinal)
            || existing.Contains($"IRepository<{entity.NamePascal},", StringComparison.Ordinal));
    }

    private void GenerateRepositoryImplementation(ProjectLayout layout, EntityDefinition entity, string dbContextName)
    {
        string dir = Path.Combine(layout.PersistencePath, "Repositories");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, $"{entity.NamePascal}Repository.cs");

        StringBuilder sb = new();
        sb.AppendLine("using Core.Persistence.Repositories;");
        sb.AppendLine($"using {layout.ProjectName}.Application.Services.Repositories;");
        sb.AppendLine($"using {layout.ProjectName}.Domain.Entities;");
        sb.AppendLine($"using {layout.ProjectName}.Persistence.Contexts;");
        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ProjectName}.Persistence.Repositories;");
        sb.AppendLine();
        sb.AppendLine($"public class {entity.NamePascal}Repository : EfRepositoryBase<{entity.NamePascal}, {entity.IdType}, {dbContextName}>, I{entity.NamePascal}Repository");
        sb.AppendLine("{");
        sb.AppendLine($"    public {entity.NamePascal}Repository({dbContextName} context)");
        sb.AppendLine("        : base(context) { }");
        sb.AppendLine("}");

        WriteFileIfNeeded(filePath, sb.ToString(), existing =>
            existing.Contains("global::", StringComparison.Ordinal)
            || existing.Contains($"EfRepositoryBase<{layout.ProjectName}.Domain.Entities.{entity.NamePascal}", StringComparison.Ordinal)
            || existing.Contains($"EfRepositoryBase<{entity.NamePascal}, {entity.IdType}", StringComparison.Ordinal));
    }

    private void RegisterRepository(ProjectLayout layout, EntityDefinition entity)
    {
        string filePath = Path.Combine(layout.PersistencePath, "PersistenceServiceRegistration.cs");
        if (!File.Exists(filePath))
            return;

        // Fix legacy or mis-typed service usings like ...Services.{Plural}; -> ...Services.{Plural}Service;
        try
        {
            string[] lines = File.ReadAllLines(filePath);
            string legacyUsing = $"using {layout.ProjectName}.Application.Services.{entity.PluralPascal};";
            string correctedUsing = $"using {layout.ProjectName}.Application.Services.{entity.PluralPascal}Service;";
            bool replaced = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().Equals(legacyUsing, StringComparison.Ordinal))
                {
                    lines[i] = correctedUsing;
                    replaced = true;
                }
            }
            if (replaced)
                File.WriteAllLines(filePath, lines);
        }
        catch
        {
            // best-effort; ignore
        }

        FileUpdateHelper.AddUsingIfMissing(filePath, $"using {layout.ProjectName}.Application.Services.Repositories;");
        FileUpdateHelper.AddUsingIfMissing(filePath, $"using {layout.ProjectName}.Persistence.Repositories;");
        FileUpdateHelper.InsertBefore(filePath, "return services;", $"        services.AddScoped<I{entity.NamePascal}Repository, {entity.NamePascal}Repository>();");
    }

    private void UpdateDbContext(ProjectLayout layout, EntityDefinition entity, string dbContextName)
    {
        string filePath = Path.Combine(layout.PersistencePath, "Contexts", $"{dbContextName}.cs");
        if (!File.Exists(filePath))
            return;

        FileUpdateHelper.AddUsingIfMissing(filePath, $"using {layout.ProjectName}.Domain.Entities;");
        string targetDbSetLine = $"    public DbSet<{entity.NamePascal}> {entity.PluralPascal} {{ get; set; }}";
        string fqnTypeMarker = $"DbSet<{layout.ProjectName}.Domain.Entities.{entity.NamePascal}>";

        // Load after potential using insertion
        List<string> lines = File.ReadAllLines(filePath).ToList();

        bool changed = false;

        // 1) Remove legacy fully-qualified DbSet lines, e.g., DbSet<Project.Domain.Entities.Entity>
        int beforeCount = lines.Count;
        lines = lines.Where(l => !l.Contains(fqnTypeMarker, StringComparison.Ordinal)).ToList();
        if (lines.Count != beforeCount)
            changed = true;

        // 1b) Also remove short-typed DbSet lines for the same property; they will be normalized to alias form
        string shortTypeMarker = $"DbSet<{entity.NamePascal}>";
        beforeCount = lines.Count;
        lines = lines.Where(l => !(l.Contains(shortTypeMarker, StringComparison.Ordinal) && l.Contains($" {entity.PluralPascal} ", StringComparison.Ordinal))).ToList();
        if (lines.Count != beforeCount)
            changed = true;

        // 2) Ensure exactly one short-typed DbSet line exists. If missing, insert near other DbSets.
        bool hasTarget = lines.Any(line => line.Contains(targetDbSetLine, StringComparison.Ordinal));
        if (!hasTarget)
        {
            int index = lines.FindLastIndex(line => line.Contains("public DbSet", StringComparison.Ordinal));
            if (index == -1)
                index = lines.FindLastIndex(line => line.Contains("{", StringComparison.Ordinal));

            if (index == -1)
                index = lines.Count - 1; // fallback append at end if structure unexpected

            lines.Insert(index + 1, targetDbSetLine);
            changed = true;
        }

        // 3) If duplicates of the exact short line somehow exist, keep the first and remove the rest
        int firstIndex = lines.FindIndex(l => l.Contains(targetDbSetLine, StringComparison.Ordinal));
        if (firstIndex != -1)
        {
            for (int i = lines.Count - 1; i > firstIndex; i--)
            {
                if (lines[i].Contains(targetDbSetLine, StringComparison.Ordinal))
                {
                    lines.RemoveAt(i);
                    changed = true;
                }
            }
        }

        if (changed)
            File.WriteAllLines(filePath, lines);
    }

    private void GenerateApplicationArtifacts(ProjectLayout layout, EntityDefinition entity, CrudGenerationOptions options)
    {
        string featureRoot = Path.Combine(layout.ApplicationPath, "Features", entity.PluralPascal);
        Directory.CreateDirectory(featureRoot);

        GenerateCreateCommand(featureRoot, layout.ProjectName, entity, options.EnableSecurity);
        GenerateUpdateCommand(featureRoot, layout.ProjectName, entity, options.EnableSecurity);
        GenerateDeleteCommand(featureRoot, layout.ProjectName, entity, options.EnableSecurity);
        GenerateGetByIdQuery(featureRoot, layout.ProjectName, entity);
        GenerateGetListQuery(featureRoot, layout.ProjectName, entity);
        GenerateFeatureConstants(featureRoot, layout.ProjectName, entity, options.EnableSecurity);
        GenerateFeatureResources(featureRoot, layout.ProjectName, entity);
        GenerateBusinessRules(featureRoot, layout.ProjectName, entity);
        GenerateMappingProfiles(featureRoot, layout.ProjectName, entity);
    }

    private void GenerateCreateCommand(string featureRoot, string projectName, EntityDefinition entity, bool enableSecurity)
    {
        string commandDir = Path.Combine(featureRoot, "Commands", "Create");
        Directory.CreateDirectory(commandDir);

        string commandPath = Path.Combine(commandDir, $"Create{entity.NamePascal}Command.cs");
        if (!File.Exists(commandPath))
        {
            StringBuilder sb = new();
            sb.AppendLine("using AutoMapper;");
            sb.AppendLine("using MediatR;");
            sb.AppendLine($"using {projectName}.Domain.Entities;");
            sb.AppendLine($"using {projectName}.Application.Services.Repositories;");
            sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Rules;");

            if (enableSecurity)
            {
                sb.AppendLine("using Core.Application.Pipelines.Authorization;");
                sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Constants;");
                sb.AppendLine();
                sb.AppendLine($"using static {projectName}.Application.Features.{entity.PluralPascal}.Constants.{entity.PluralPascal}OperationClaims;");
            }

            sb.AppendLine();
            sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Commands.Create;");
            sb.AppendLine();

            string interfaceList = enableSecurity ?
                $"IRequest<Created{entity.NamePascal}Response>, ISecuredRequest" :
                $"IRequest<Created{entity.NamePascal}Response>";
            sb.AppendLine($"public class Create{entity.NamePascal}Command : {interfaceList}");
            sb.AppendLine("{");
            foreach (PropertyDefinition property in entity.Properties)
                sb.AppendLine(FormatProperty(property));

            sb.AppendLine();
            sb.AppendLine($"    public Create{entity.NamePascal}Command()");
            sb.AppendLine("    {");
            // Initialize all string properties with string.Empty for safety
            foreach (PropertyDefinition property in entity.Properties)
            {
                if (property.NonNullableType.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"        {property.PropertyName} = string.Empty;");
                }
            }
            sb.AppendLine("    }");

            // Parameterized constructor
            sb.AppendLine();
            var createParameters = entity.Properties
                .Select(p => $"{p.Type} {p.PropertyName.ToCamelCase()}")
                .ToList();

            sb.AppendLine($"    public Create{entity.NamePascal}Command({string.Join(", ", createParameters)})");
            sb.AppendLine("    {");
            foreach (PropertyDefinition property in entity.Properties)
            {
                sb.AppendLine($"        {property.PropertyName} = {property.PropertyName.ToCamelCase()};");
            }
            sb.AppendLine("    }");

            if (enableSecurity)
            {
                sb.AppendLine();
                sb.AppendLine($"    public string[] Roles => new[] {{ Admin, Write, {entity.PluralPascal}OperationClaims.Create }};");
            }

            sb.AppendLine();
            sb.AppendLine($"    public class Create{entity.NamePascal}CommandHandler : IRequestHandler<Create{entity.NamePascal}Command, Created{entity.NamePascal}Response>");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly I{entity.NamePascal}Repository _{entity.NameCamel}Repository;");
            sb.AppendLine("        private readonly IMapper _mapper;");
            sb.AppendLine($"        private readonly {entity.NamePascal}BusinessRules _{entity.NameCamel}BusinessRules;");
            sb.AppendLine();
            sb.AppendLine($"        public Create{entity.NamePascal}CommandHandler(I{entity.NamePascal}Repository {entity.NameCamel}Repository, IMapper mapper, {entity.NamePascal}BusinessRules {entity.NameCamel}BusinessRules)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _{entity.NameCamel}Repository = {entity.NameCamel}Repository;");
            sb.AppendLine("            _mapper = mapper;");
            sb.AppendLine($"            _{entity.NameCamel}BusinessRules = {entity.NameCamel}BusinessRules;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public async Task<Created{entity.NamePascal}Response> Handle(Create{entity.NamePascal}Command request, CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {entity.NamePascal} mapped{entity.NamePascal} = _mapper.Map<{entity.NamePascal}>(request);");
            sb.AppendLine($"            {entity.NamePascal} created{entity.NamePascal} = await _{entity.NameCamel}Repository.AddAsync(mapped{entity.NamePascal}, cancellationToken: cancellationToken);");
            sb.AppendLine($"            Created{entity.NamePascal}Response response = _mapper.Map<Created{entity.NamePascal}Response>(created{entity.NamePascal});");
            sb.AppendLine("            return response;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(commandPath, sb.ToString());
        }

        string responsePath = Path.Combine(commandDir, $"Created{entity.NamePascal}Response.cs");
        string createdResponseContent = BuildResponseClass(projectName, entity, "Commands.Create", $"Created{entity.NamePascal}Response");
        WriteFileIfNeeded(responsePath, createdResponseContent, existing =>
            existing.Contains("Commands.Creates", StringComparison.Ordinal));
    }

    private void GenerateUpdateCommand(string featureRoot, string projectName, EntityDefinition entity, bool enableSecurity)
    {
        string commandDir = Path.Combine(featureRoot, "Commands", "Update");
        Directory.CreateDirectory(commandDir);

        string commandPath = Path.Combine(commandDir, $"Update{entity.NamePascal}Command.cs");
        if (!File.Exists(commandPath))
        {
            StringBuilder sb = new();
            sb.AppendLine("using AutoMapper;");
            sb.AppendLine("using MediatR;");
            sb.AppendLine($"using {projectName}.Domain.Entities;");
            sb.AppendLine($"using {projectName}.Application.Services.Repositories;");
            sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Rules;");

            if (enableSecurity)
            {
                sb.AppendLine("using Core.Application.Pipelines.Authorization;");
                sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Constants;");
                sb.AppendLine();
                sb.AppendLine($"using static {projectName}.Application.Features.{entity.PluralPascal}.Constants.{entity.PluralPascal}OperationClaims;");
            }
            sb.AppendLine();
            sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Commands.Update;");
            sb.AppendLine();

            string interfaceList = enableSecurity ?
                $"IRequest<Updated{entity.NamePascal}Response>, ISecuredRequest" :
                $"IRequest<Updated{entity.NamePascal}Response>";
            sb.AppendLine($"public class Update{entity.NamePascal}Command : {interfaceList}");
            sb.AppendLine("{");
            sb.AppendLine($"    public {entity.IdType} Id {{ get; set; }}");
            foreach (PropertyDefinition property in entity.Properties)
                sb.AppendLine(FormatProperty(property));

            sb.AppendLine();
            sb.AppendLine($"    public Update{entity.NamePascal}Command()");
            sb.AppendLine("    {");
            // Initialize all string properties with string.Empty for safety
            foreach (PropertyDefinition property in entity.Properties)
            {
                if (property.NonNullableType.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"        {property.PropertyName} = string.Empty;");
                }
            }
            sb.AppendLine("    }");

            // Parameterized constructor
            sb.AppendLine();
            var parameters = entity.Properties
                .Select(p => $"{p.Type} {p.PropertyName.ToCamelCase()}")
                .ToList();
            parameters.Insert(0, $"{entity.IdType} id");

            sb.AppendLine($"    public Update{entity.NamePascal}Command({string.Join(", ", parameters)})");
            sb.AppendLine("    {");
            sb.AppendLine("        Id = id;");
            foreach (PropertyDefinition property in entity.Properties)
            {
                sb.AppendLine($"        {property.PropertyName} = {property.PropertyName.ToCamelCase()};");
            }
            sb.AppendLine("    }");

            if (enableSecurity)
            {
                sb.AppendLine();
                sb.AppendLine($"    public string[] Roles => new[] {{ Admin, Write, {entity.PluralPascal}OperationClaims.Update }};");
            }

            sb.AppendLine();
            sb.AppendLine($"    public class Update{entity.NamePascal}CommandHandler : IRequestHandler<Update{entity.NamePascal}Command, Updated{entity.NamePascal}Response>");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly I{entity.NamePascal}Repository _{entity.NameCamel}Repository;");
            sb.AppendLine("        private readonly IMapper _mapper;");
            sb.AppendLine($"        private readonly {entity.NamePascal}BusinessRules _{entity.NameCamel}BusinessRules;");
            sb.AppendLine();
            sb.AppendLine($"        public Update{entity.NamePascal}CommandHandler(I{entity.NamePascal}Repository {entity.NameCamel}Repository, IMapper mapper, {entity.NamePascal}BusinessRules {entity.NameCamel}BusinessRules)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _{entity.NameCamel}Repository = {entity.NameCamel}Repository;");
            sb.AppendLine("            _mapper = mapper;");
            sb.AppendLine($"            _{entity.NameCamel}BusinessRules = {entity.NameCamel}BusinessRules;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public async Task<Updated{entity.NamePascal}Response> Handle(Update{entity.NamePascal}Command request, CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {entity.NamePascal} mapped{entity.NamePascal} = _mapper.Map<{entity.NamePascal}>(request);");
            sb.AppendLine($"            {entity.NamePascal} updated{entity.NamePascal} = await _{entity.NameCamel}Repository.UpdateAsync(mapped{entity.NamePascal}, cancellationToken: cancellationToken);");
            sb.AppendLine($"            Updated{entity.NamePascal}Response response = _mapper.Map<Updated{entity.NamePascal}Response>(updated{entity.NamePascal});");
            sb.AppendLine("            return response;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(commandPath, sb.ToString());
        }

        string responsePath = Path.Combine(commandDir, $"Updated{entity.NamePascal}Response.cs");
        string updatedResponseContent = BuildResponseClass(projectName, entity, "Commands.Update", $"Updated{entity.NamePascal}Response");
        WriteFileIfNeeded(responsePath, updatedResponseContent, existing =>
            existing.Contains("Commands.Updates", StringComparison.Ordinal));
    }

    private void GenerateDeleteCommand(string featureRoot, string projectName, EntityDefinition entity, bool enableSecurity)
    {
        string commandDir = Path.Combine(featureRoot, "Commands", "Delete");
        Directory.CreateDirectory(commandDir);

        string commandPath = Path.Combine(commandDir, $"Delete{entity.NamePascal}Command.cs");
        if (!File.Exists(commandPath))
        {
            StringBuilder sb = new();
            sb.AppendLine("using AutoMapper;");
            sb.AppendLine("using MediatR;");
            sb.AppendLine($"using {projectName}.Domain.Entities;");
            sb.AppendLine($"using {projectName}.Application.Services.Repositories;");
            sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Rules;");

            if (enableSecurity)
            {
                sb.AppendLine("using Core.Application.Pipelines.Authorization;");
                sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Constants;");
                sb.AppendLine();
                sb.AppendLine($"using static {projectName}.Application.Features.{entity.PluralPascal}.Constants.{entity.PluralPascal}OperationClaims;");
            }

            sb.AppendLine();
            sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Commands.Delete;");
            sb.AppendLine();

            string interfaceList = enableSecurity ?
                $"IRequest<Deleted{entity.NamePascal}Response>, ISecuredRequest" :
                $"IRequest<Deleted{entity.NamePascal}Response>";
            sb.AppendLine($"public class Delete{entity.NamePascal}Command : {interfaceList}");
            sb.AppendLine("{");
            sb.AppendLine($"    public {entity.IdType} Id {{ get; set; }}");

            if (enableSecurity)
            {
                sb.AppendLine();
                sb.AppendLine($"    public string[] Roles => new[] {{ Admin, Write, {entity.PluralPascal}OperationClaims.Delete }};");
            }

            sb.AppendLine();
            sb.AppendLine($"    public class Delete{entity.NamePascal}CommandHandler : IRequestHandler<Delete{entity.NamePascal}Command, Deleted{entity.NamePascal}Response>");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly I{entity.NamePascal}Repository _{entity.NameCamel}Repository;");
            sb.AppendLine("        private readonly IMapper _mapper;");
            sb.AppendLine($"        private readonly {entity.NamePascal}BusinessRules _{entity.NameCamel}BusinessRules;");
            sb.AppendLine();
            sb.AppendLine($"        public Delete{entity.NamePascal}CommandHandler(I{entity.NamePascal}Repository {entity.NameCamel}Repository, IMapper mapper, {entity.NamePascal}BusinessRules {entity.NameCamel}BusinessRules)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _{entity.NameCamel}Repository = {entity.NameCamel}Repository;");
            sb.AppendLine("            _mapper = mapper;");
            sb.AppendLine($"            _{entity.NameCamel}BusinessRules = {entity.NameCamel}BusinessRules;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public async Task<Deleted{entity.NamePascal}Response> Handle(Delete{entity.NamePascal}Command request, CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {entity.NamePascal}? {entity.NameCamel} = await _{entity.NameCamel}Repository.GetAsync(predicate: {entity.NameCamel.ToLower()} => {entity.NameCamel.ToLower()}.Id == request.Id, cancellationToken: cancellationToken);");
            sb.AppendLine($"            await _{entity.NameCamel}BusinessRules.{entity.NamePascal}ShouldExistWhenSelected({entity.NameCamel});");
            sb.AppendLine();
            sb.AppendLine($"            await _{entity.NameCamel}Repository.DeleteAsync({entity.NameCamel}!, cancellationToken: cancellationToken);");
            sb.AppendLine();
            sb.AppendLine($"            Deleted{entity.NamePascal}Response response = _mapper.Map<Deleted{entity.NamePascal}Response>({entity.NameCamel});");
            sb.AppendLine("            return response;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(commandPath, sb.ToString());
        }

        string responsePath = Path.Combine(commandDir, $"Deleted{entity.NamePascal}Response.cs");
        if (!File.Exists(responsePath))
            File.WriteAllText(responsePath, BuildDeletedResponseClass(projectName, entity));
    }

    private void GenerateGetByIdQuery(string featureRoot, string projectName, EntityDefinition entity)
    {
        string queryDir = Path.Combine(featureRoot, "Queries", "GetById");
        Directory.CreateDirectory(queryDir);

        string queryPath = Path.Combine(queryDir, $"GetById{entity.NamePascal}Query.cs");
        if (!File.Exists(queryPath))
        {
            string serviceNamespace = $"{projectName}.Application.Services.{entity.PluralPascal}Service";

            StringBuilder sb = new();
            sb.AppendLine("using AutoMapper;");
            sb.AppendLine("using MediatR;");
            sb.AppendLine($"using {projectName}.Domain.Entities;");
            sb.AppendLine($"using {projectName}.Application.Services.Repositories;");
            sb.AppendLine();
            sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Queries.GetById;");
            sb.AppendLine();
            sb.AppendLine($"public class GetById{entity.NamePascal}Query : IRequest<GetById{entity.NamePascal}Response>");
            sb.AppendLine("{");
            sb.AppendLine($"    public {entity.IdType} Id {{ get; set; }}");
            sb.AppendLine();
            sb.AppendLine($"    public class GetById{entity.NamePascal}QueryHandler : IRequestHandler<GetById{entity.NamePascal}Query, GetById{entity.NamePascal}Response>");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly I{entity.NamePascal}Repository _{entity.NameCamel}Repository;");
            sb.AppendLine("        private readonly IMapper _mapper;");
            sb.AppendLine();
            sb.AppendLine($"        public GetById{entity.NamePascal}QueryHandler(I{entity.NamePascal}Repository {entity.NameCamel}Repository, IMapper mapper)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _{entity.NameCamel}Repository = {entity.NameCamel}Repository;");
            sb.AppendLine("            _mapper = mapper;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public async Task<GetById{entity.NamePascal}Response> Handle(GetById{entity.NamePascal}Query request, CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {entity.NamePascal}? {entity.NameCamel} = await _{entity.NameCamel}Repository.GetAsync(predicate: {entity.NameCamel.ToLower()} => {entity.NameCamel.ToLower()}.Id == request.Id, cancellationToken: cancellationToken);");
            sb.AppendLine($"            GetById{entity.NamePascal}Response response = _mapper.Map<GetById{entity.NamePascal}Response>({entity.NameCamel});");
            sb.AppendLine("            return response;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(queryPath, sb.ToString());
        }

        string responsePath = Path.Combine(queryDir, $"GetById{entity.NamePascal}Response.cs");
        if (!File.Exists(responsePath))
            File.WriteAllText(responsePath, BuildResponseClass(projectName, entity, "Queries.GetById", $"GetById{entity.NamePascal}Response"));
    }

    private void GenerateGetListQuery(string featureRoot, string projectName, EntityDefinition entity)
    {
        string queryDir = Path.Combine(featureRoot, "Queries", "GetList");
        Directory.CreateDirectory(queryDir);

        string queryPath = Path.Combine(queryDir, $"GetList{entity.NamePascal}Query.cs");
        if (!File.Exists(queryPath))
        {
            StringBuilder sb = new();
            sb.AppendLine("using AutoMapper;");
            sb.AppendLine("using MediatR;");
            sb.AppendLine("using Core.Application.Requests;");
            sb.AppendLine("using Core.Application.Responses;");
            sb.AppendLine("using Core.Persistence.Paging;");
            sb.AppendLine($"using {projectName}.Domain.Entities;");
            sb.AppendLine($"using {projectName}.Application.Services.Repositories;");
            sb.AppendLine();
            sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Queries.GetList;");
            sb.AppendLine();
            sb.AppendLine($"public class GetList{entity.NamePascal}Query : IRequest<GetListResponse<GetList{entity.NamePascal}ListItemDto>>");
            sb.AppendLine("{");
            sb.AppendLine("    public PageRequest PageRequest { get; set; } = new();");
            sb.AppendLine();
            sb.AppendLine($"    public class GetList{entity.NamePascal}QueryHandler : IRequestHandler<GetList{entity.NamePascal}Query, GetListResponse<GetList{entity.NamePascal}ListItemDto>>");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly I{entity.NamePascal}Repository _{entity.NameCamel}Repository;");
            sb.AppendLine("        private readonly IMapper _mapper;");
            sb.AppendLine();
            sb.AppendLine($"        public GetList{entity.NamePascal}QueryHandler(I{entity.NamePascal}Repository {entity.NameCamel}Repository, IMapper mapper)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _{entity.NameCamel}Repository = {entity.NameCamel}Repository;");
            sb.AppendLine("            _mapper = mapper;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public async Task<GetListResponse<GetList{entity.NamePascal}ListItemDto>> Handle(GetList{entity.NamePascal}Query request, CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine($"            IPaginate<{entity.NamePascal}> {entity.NameCamel}s = await _{entity.NameCamel}Repository.GetListAsync(");
            sb.AppendLine("                index: request.PageRequest.PageIndex,");
            sb.AppendLine("                size: request.PageRequest.PageSize,");
            sb.AppendLine("                cancellationToken: cancellationToken");
            sb.AppendLine("            );");
            sb.AppendLine($"            GetListResponse<GetList{entity.NamePascal}ListItemDto> response = _mapper.Map<GetListResponse<GetList{entity.NamePascal}ListItemDto>>({entity.NameCamel}s);");
            sb.AppendLine("            return response;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(queryPath, sb.ToString());
        }

        string dtoPath = Path.Combine(queryDir, $"GetList{entity.NamePascal}ListItemDto.cs");
        if (!File.Exists(dtoPath))
        {
            StringBuilder sb = new();
            sb.AppendLine("using Core.Application.Dtos;");
            sb.AppendLine();
            sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Queries.GetList;");
            sb.AppendLine();
            sb.AppendLine($"public class GetList{entity.NamePascal}ListItemDto : IDto");
            sb.AppendLine("{");
            sb.AppendLine($"    public {entity.IdType} Id {{ get; set; }}");
            foreach (PropertyDefinition property in entity.Properties)
                sb.AppendLine(FormatProperty(property));
            sb.AppendLine();
            sb.AppendLine($"    public GetList{entity.NamePascal}ListItemDto()");
            sb.AppendLine("    {");
            // Initialize all string properties with string.Empty for safety
            foreach (PropertyDefinition property in entity.Properties)
            {
                if (property.NonNullableType.Equals("string", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"        {property.PropertyName} = string.Empty;");
                }
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(dtoPath, sb.ToString());
        }
    }

    private void GenerateBusinessRules(string featureRoot, string projectName, EntityDefinition entity)
    {
        string rulesDir = Path.Combine(featureRoot, "Rules");
        Directory.CreateDirectory(rulesDir);
        string filePath = Path.Combine(rulesDir, $"{entity.NamePascal}BusinessRules.cs");

        StringBuilder sb = new();
        sb.AppendLine("using Core.Application.Rules;");
        sb.AppendLine("using Core.CrossCuttingConcerns.Exception.Types;");
        sb.AppendLine("using Core.Localization.Abstraction;");
        sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Constants;");
        sb.AppendLine($"using {projectName}.Application.Services.Repositories;");
        sb.AppendLine($"using {projectName}.Domain.Entities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Rules;");
        sb.AppendLine();
        sb.AppendLine($"public class {entity.NamePascal}BusinessRules : BaseBusinessRules");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly I{entity.NamePascal}Repository _{entity.NameCamel}Repository;");
        sb.AppendLine("    private readonly ILocalizationService _localizationService;");
        sb.AppendLine();
        sb.AppendLine($"    public {entity.NamePascal}BusinessRules(");
        sb.AppendLine($"        I{entity.NamePascal}Repository {entity.NameCamel}Repository,");
        sb.AppendLine("        ILocalizationService localizationService");
        sb.AppendLine("    )");
        sb.AppendLine("    {");
        sb.AppendLine($"        _{entity.NameCamel}Repository = {entity.NameCamel}Repository;");
        sb.AppendLine("        _localizationService = localizationService;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private async Task throwBusinessException(string messageKey)");
        sb.AppendLine("    {");
        sb.AppendLine($"        string message = await _localizationService.GetLocalizedAsync(messageKey, {entity.PluralPascal}Messages.SectionName);");
        sb.AppendLine("        throw new BusinessException(message);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public async Task {entity.NamePascal}ShouldExistWhenSelected({entity.NamePascal}? {entity.NameCamel})");
        sb.AppendLine("    {");
        sb.AppendLine($"        if ({entity.NameCamel} == null)");
        sb.AppendLine($"            await throwBusinessException({entity.PluralPascal}Messages.{entity.NamePascal}NotExists);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public async Task {entity.NamePascal}IdShouldExistWhenSelected({entity.IdType} id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        bool doesExist = await _{entity.NameCamel}Repository.AnyAsync(predicate: {entity.NameCamel.ToLower()} => {entity.NameCamel.ToLower()}.Id == id);");
        sb.AppendLine("        if (!doesExist)");
        sb.AppendLine($"            await throwBusinessException({entity.PluralPascal}Messages.{entity.NamePascal}NotExists);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        WriteFileIfNeeded(filePath, sb.ToString(), existing =>
            existing.Contains("Core.CrossCuttingConcerns.Exceptions", StringComparison.Ordinal));
    }

    private void GenerateMappingProfiles(string featureRoot, string projectName, EntityDefinition entity)
    {
        string profilesDir = Path.Combine(featureRoot, "Profiles");
        Directory.CreateDirectory(profilesDir);
        string filePath = Path.Combine(profilesDir, "MappingProfiles.cs");
        if (File.Exists(filePath))
            return;

        StringBuilder sb = new();
        sb.AppendLine("using AutoMapper;");
        sb.AppendLine("using Core.Application.Responses;");
        sb.AppendLine("using Core.Persistence.Paging;");
        sb.AppendLine($"using {projectName}.Domain.Entities;");
        sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Commands.Create;");
        sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Commands.Update;");
        sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Commands.Delete;");
        sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Queries.GetById;");
        sb.AppendLine($"using {projectName}.Application.Features.{entity.PluralPascal}.Queries.GetList;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Profiles;");
        sb.AppendLine();
        sb.AppendLine("public class MappingProfiles : Profile");
        sb.AppendLine("{");
        sb.AppendLine("    public MappingProfiles()");
        sb.AppendLine("    {");
        sb.AppendLine($"        CreateMap<{entity.NamePascal}, Create{entity.NamePascal}Command>().ReverseMap();");
        sb.AppendLine($"        CreateMap<{entity.NamePascal}, Created{entity.NamePascal}Response>().ReverseMap();");
        sb.AppendLine($"        CreateMap<{entity.NamePascal}, Update{entity.NamePascal}Command>().ReverseMap();");
        sb.AppendLine($"        CreateMap<{entity.NamePascal}, Updated{entity.NamePascal}Response>().ReverseMap();");
        sb.AppendLine($"        CreateMap<{entity.NamePascal}, Delete{entity.NamePascal}Command>().ReverseMap();");
        sb.AppendLine($"        CreateMap<{entity.NamePascal}, Deleted{entity.NamePascal}Response>().ReverseMap();");
        sb.AppendLine($"        CreateMap<{entity.NamePascal}, GetById{entity.NamePascal}Response>().ReverseMap();");
        sb.AppendLine($"        CreateMap<{entity.NamePascal}, GetList{entity.NamePascal}ListItemDto>().ReverseMap();");
        sb.AppendLine($"        CreateMap<IPaginate<{entity.NamePascal}>, GetListResponse<GetList{entity.NamePascal}ListItemDto>>().ReverseMap();");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        File.WriteAllText(filePath, sb.ToString());
    }

    private void GenerateFeatureConstants(string featureRoot, string projectName, EntityDefinition entity, bool enableSecurity)
    {
        string constantsDir = Path.Combine(featureRoot, "Constants");
        Directory.CreateDirectory(constantsDir);

        // Only generate operation claims if security is enabled
        if (enableSecurity)
        {
            string operationClaimsPath = Path.Combine(constantsDir, $"{entity.PluralPascal}OperationClaims.cs");
            if (!File.Exists(operationClaimsPath))
            {
                StringBuilder sb = new();
                sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Constants;");
                sb.AppendLine();
                sb.AppendLine($"public static class {entity.PluralPascal}OperationClaims");
                sb.AppendLine("{");
                sb.AppendLine($"    private const string Section = \"{entity.PluralPascal}\";");
                sb.AppendLine();
                sb.AppendLine("    public const string Admin = $\"{Section}.Admin\";");
                sb.AppendLine("    public const string Read = $\"{Section}.Read\";");
                sb.AppendLine("    public const string Write = $\"{Section}.Write\";");
                sb.AppendLine("    public const string Create = $\"{Section}.Create\";");
                sb.AppendLine("    public const string Update = $\"{Section}.Update\";");
                sb.AppendLine("    public const string Delete = $\"{Section}.Delete\";");
                sb.AppendLine("}");

                File.WriteAllText(operationClaimsPath, sb.ToString());
            }
        }

        string messagesPath = Path.Combine(constantsDir, $"{entity.PluralPascal}Messages.cs");
        if (!File.Exists(messagesPath))
        {
            StringBuilder sb = new();
            sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Constants;");
            sb.AppendLine();
            sb.AppendLine($"public static class {entity.PluralPascal}Messages");
            sb.AppendLine("{");
            sb.AppendLine($"    public const string SectionName = \"{entity.PluralPascal}\";");
            sb.AppendLine($"    public const string {entity.NamePascal}NotExists = \"{entity.NamePascal}NotExists\";");
            sb.AppendLine("}");

            File.WriteAllText(messagesPath, sb.ToString());
        }
    }

    private void GenerateFeatureResources(string featureRoot, string projectName, EntityDefinition entity)
    {
        string resourcesDir = Path.Combine(featureRoot, "Resources", "Locales");
        Directory.CreateDirectory(resourcesDir);

        string fileName = entity.PluralCamel.ToLowerInvariant();

        string enPath = Path.Combine(resourcesDir, $"{fileName}.en.yaml");
        if (!File.Exists(enPath))
        {
            string content = $"{entity.NamePascal}NotExists: \"{entity.NamePascal} not found.\"";
            File.WriteAllText(enPath, content);
        }

        string trPath = Path.Combine(resourcesDir, $"{fileName}.tr.yaml");
        if (!File.Exists(trPath))
        {
            string content = $"{entity.NamePascal}NotExists: \"{entity.NamePascal} bulunamadı.\"";
            File.WriteAllText(trPath, content);
        }
    }

    private void GenerateServices(ProjectLayout layout, EntityDefinition entity)
    {
        string serviceFolder = $"{entity.PluralPascal}Service";
        string servicesRoot = Path.Combine(layout.ApplicationPath, "Services", serviceFolder);
        string legacyServicesRoot = Path.Combine(layout.ApplicationPath, "Services", entity.PluralPascal);

        if (Directory.Exists(legacyServicesRoot) && !Directory.Exists(servicesRoot))
            Directory.Move(legacyServicesRoot, servicesRoot);

        Directory.CreateDirectory(servicesRoot);

        string interfacePath = Path.Combine(servicesRoot, $"I{entity.NamePascal}Service.cs");
        StringBuilder interfaceBuilder = new();
        interfaceBuilder.AppendLine("using System.Linq.Expressions;");
        interfaceBuilder.AppendLine();
        interfaceBuilder.AppendLine("using Core.Persistence.Paging;");
        interfaceBuilder.AppendLine();
        interfaceBuilder.AppendLine("using Microsoft.EntityFrameworkCore.Query;");
        interfaceBuilder.AppendLine();
        interfaceBuilder.AppendLine($"using {layout.ProjectName}.Domain.Entities;");
        interfaceBuilder.AppendLine();
        interfaceBuilder.AppendLine($"namespace {layout.ProjectName}.Application.Services.{entity.PluralPascal}Service;");
        interfaceBuilder.AppendLine();
        interfaceBuilder.AppendLine($"public interface I{entity.NamePascal}Service");
        interfaceBuilder.AppendLine("{");
        interfaceBuilder.AppendLine($"    Task<{entity.NamePascal}?> GetAsync(");
        interfaceBuilder.AppendLine($"        Expression<Func<{entity.NamePascal}, bool>> predicate,");
        interfaceBuilder.AppendLine($"        Func<IQueryable<{entity.NamePascal}>, IIncludableQueryable<{entity.NamePascal}, object>>? include = null,");
        interfaceBuilder.AppendLine("        bool withDeleted = false,");
        interfaceBuilder.AppendLine("        bool enableTracking = true,");
        interfaceBuilder.AppendLine("        CancellationToken cancellationToken = default");
        interfaceBuilder.AppendLine("    );");
        interfaceBuilder.AppendLine();
        interfaceBuilder.AppendLine($"    Task<IPaginate<{entity.NamePascal}>?> GetListAsync(");
        interfaceBuilder.AppendLine($"        Expression<Func<{entity.NamePascal}, bool>>? predicate = null,");
        interfaceBuilder.AppendLine($"        Func<IQueryable<{entity.NamePascal}>, IOrderedQueryable<{entity.NamePascal}>>? orderBy = null,");
        interfaceBuilder.AppendLine($"        Func<IQueryable<{entity.NamePascal}>, IIncludableQueryable<{entity.NamePascal}, object>>? include = null,");
        interfaceBuilder.AppendLine("        int index = 0,");
        interfaceBuilder.AppendLine("        int size = 10,");
        interfaceBuilder.AppendLine("        bool withDeleted = false,");
        interfaceBuilder.AppendLine("        bool enableTracking = true,");
        interfaceBuilder.AppendLine("        CancellationToken cancellationToken = default");
        interfaceBuilder.AppendLine("    );");
        interfaceBuilder.AppendLine();
        interfaceBuilder.AppendLine($"    Task<{entity.NamePascal}> AddAsync({entity.NamePascal} {entity.NameCamel});");
        interfaceBuilder.AppendLine($"    Task<{entity.NamePascal}> UpdateAsync({entity.NamePascal} {entity.NameCamel});");
        interfaceBuilder.AppendLine($"    Task<{entity.NamePascal}> DeleteAsync({entity.NamePascal} {entity.NameCamel}, bool permanent = false);");
        interfaceBuilder.AppendLine("}");

        WriteFileIfNeeded(interfacePath, interfaceBuilder.ToString(), existing =>
            existing.Contains("global::", StringComparison.Ordinal)
            || existing.Contains($"namespace {layout.ProjectName}.Application.Services.{entity.PluralPascal};", StringComparison.Ordinal));

        string managerPath = Path.Combine(servicesRoot, $"{entity.NamePascal}Manager.cs");
        StringBuilder managerBuilder = new();
        managerBuilder.AppendLine("using System.Linq.Expressions;");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine("using Core.Persistence.Paging;");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine("using Microsoft.EntityFrameworkCore.Query;");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"using {layout.ProjectName}.Application.Features.{entity.PluralPascal}.Rules;");
        managerBuilder.AppendLine($"using {layout.ProjectName}.Application.Services.Repositories;");
        managerBuilder.AppendLine($"using {layout.ProjectName}.Domain.Entities;");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"namespace {layout.ProjectName}.Application.Services.{entity.PluralPascal}Service;");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"public class {entity.NamePascal}Manager : I{entity.NamePascal}Service");
        managerBuilder.AppendLine("{");
        managerBuilder.AppendLine($"    private readonly I{entity.NamePascal}Repository _{entity.NameCamel}Repository;");
        managerBuilder.AppendLine($"    private readonly {entity.NamePascal}BusinessRules _{entity.NameCamel}BusinessRules;");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"    public {entity.NamePascal}Manager(I{entity.NamePascal}Repository {entity.NameCamel}Repository, {entity.NamePascal}BusinessRules {entity.NameCamel}BusinessRules)");
        managerBuilder.AppendLine("    {");
        managerBuilder.AppendLine($"        _{entity.NameCamel}Repository = {entity.NameCamel}Repository;");
        managerBuilder.AppendLine($"        _{entity.NameCamel}BusinessRules = {entity.NameCamel}BusinessRules;");
        managerBuilder.AppendLine("    }");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"    public async Task<{entity.NamePascal}?> GetAsync(");
        managerBuilder.AppendLine($"        Expression<Func<{entity.NamePascal}, bool>> predicate,");
        managerBuilder.AppendLine($"        Func<IQueryable<{entity.NamePascal}>, IIncludableQueryable<{entity.NamePascal}, object>>? include = null,");
        managerBuilder.AppendLine("        bool withDeleted = false,");
        managerBuilder.AppendLine("        bool enableTracking = true,");
        managerBuilder.AppendLine("        CancellationToken cancellationToken = default");
        managerBuilder.AppendLine("    )");
        managerBuilder.AppendLine("    {");
        managerBuilder.AppendLine($"        {entity.NamePascal}? {entity.NameCamel} = await _{entity.NameCamel}Repository.GetAsync(predicate, include, withDeleted, enableTracking, cancellationToken);");
        managerBuilder.AppendLine($"        return {entity.NameCamel};");
        managerBuilder.AppendLine("    }");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"    public async Task<IPaginate<{entity.NamePascal}>?> GetListAsync(");
        managerBuilder.AppendLine($"        Expression<Func<{entity.NamePascal}, bool>>? predicate = null,");
        managerBuilder.AppendLine($"        Func<IQueryable<{entity.NamePascal}>, IOrderedQueryable<{entity.NamePascal}>>? orderBy = null,");
        managerBuilder.AppendLine($"        Func<IQueryable<{entity.NamePascal}>, IIncludableQueryable<{entity.NamePascal}, object>>? include = null,");
        managerBuilder.AppendLine("        int index = 0,");
        managerBuilder.AppendLine("        int size = 10,");
        managerBuilder.AppendLine("        bool withDeleted = false,");
        managerBuilder.AppendLine("        bool enableTracking = true,");
        managerBuilder.AppendLine("        CancellationToken cancellationToken = default");
        managerBuilder.AppendLine("    )");
        managerBuilder.AppendLine("    {");
        managerBuilder.AppendLine($"        IPaginate<{entity.NamePascal}>? {entity.NameCamel}List = await _{entity.NameCamel}Repository.GetListAsync(");
        managerBuilder.AppendLine("            predicate,");
        managerBuilder.AppendLine("            orderBy,");
        managerBuilder.AppendLine("            include,");
        managerBuilder.AppendLine("            index,");
        managerBuilder.AppendLine("            size,");
        managerBuilder.AppendLine("            withDeleted,");
        managerBuilder.AppendLine("            enableTracking,");
        managerBuilder.AppendLine("            cancellationToken");
        managerBuilder.AppendLine("        );");
        managerBuilder.AppendLine($"        return {entity.NameCamel}List;");
        managerBuilder.AppendLine("    }");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"    public async Task<{entity.NamePascal}> AddAsync({entity.NamePascal} {entity.NameCamel})");
        managerBuilder.AppendLine("    {");
        managerBuilder.AppendLine($"        {entity.NamePascal} added{entity.NamePascal} = await _{entity.NameCamel}Repository.AddAsync({entity.NameCamel});");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"        return added{entity.NamePascal};");
        managerBuilder.AppendLine("    }");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"    public async Task<{entity.NamePascal}> UpdateAsync({entity.NamePascal} {entity.NameCamel})");
        managerBuilder.AppendLine("    {");
        managerBuilder.AppendLine($"        {entity.NamePascal} updated{entity.NamePascal} = await _{entity.NameCamel}Repository.UpdateAsync({entity.NameCamel});");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"        return updated{entity.NamePascal};");
        managerBuilder.AppendLine("    }");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"    public async Task<{entity.NamePascal}> DeleteAsync({entity.NamePascal} {entity.NameCamel}, bool permanent = false)");
        managerBuilder.AppendLine("    {");
        managerBuilder.AppendLine($"        {entity.NamePascal} deleted{entity.NamePascal} = await _{entity.NameCamel}Repository.DeleteAsync({entity.NameCamel});");
        managerBuilder.AppendLine();
        managerBuilder.AppendLine($"        return deleted{entity.NamePascal};");
        managerBuilder.AppendLine("    }");
        managerBuilder.AppendLine("}");

        WriteFileIfNeeded(managerPath, managerBuilder.ToString(), existing =>
            existing.Contains("global::", StringComparison.Ordinal)
            || existing.Contains($"namespace {layout.ProjectName}.Application.Services.{entity.PluralPascal};", StringComparison.Ordinal));

        RemoveLegacyServiceFolderIfObsolete(legacyServicesRoot, servicesRoot, entity);

        RegisterApplicationService(layout, entity);
    }

    private void RegisterApplicationService(ProjectLayout layout, EntityDefinition entity)
    {
        string filePath = Path.Combine(layout.ApplicationPath, "ApplicationServiceRegistration.cs");
        if (!File.Exists(filePath))
            return;

        // Best-effort sweep: fix any legacy/mistyped usings like
        //   using {Project}.Application.Services.{Plural};
        // to
        //   using {Project}.Application.Services.{Plural}Service;
        // but only when a Services/{Plural}Service folder exists. Avoid touching Repositories and other non-Service namespaces.
        try
        {
            string[] lines = File.ReadAllLines(filePath);
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                const string prefix = "using ";
                string expectedRoot = $"{layout.ProjectName}.Application.Services.";
                if (line.StartsWith(prefix, StringComparison.Ordinal) && line.EndsWith(";", StringComparison.Ordinal))
                {
                    string ns = line.Substring(prefix.Length, line.Length - prefix.Length - 1).Trim();
                    if (ns.StartsWith(expectedRoot, StringComparison.Ordinal))
                    {
                        string suffix = ns.Substring(expectedRoot.Length);
                        // Skip namespaces that already end with Service or are known non-service namespaces
                        if (!suffix.EndsWith("Service", StringComparison.Ordinal)
                            && !string.Equals(suffix, "Repositories", StringComparison.Ordinal))
                        {
                            string candidateFolder = Path.Combine(layout.ApplicationPath, "Services", suffix + "Service");
                            if (Directory.Exists(candidateFolder))
                            {
                                lines[i] = $"using {expectedRoot}{suffix}Service;";
                                changed = true;
                            }
                        }
                    }
                }
            }
            if (changed)
                File.WriteAllLines(filePath, lines);
        }
        catch
        {
            // ignore
        }

        FileUpdateHelper.AddUsingIfMissing(filePath, $"using {layout.ProjectName}.Application.Services.{entity.PluralPascal}Service;");
        FileUpdateHelper.AddUsingIfMissing(filePath, $"using {layout.ProjectName}.Application.Services.Repositories;");
        FileUpdateHelper.InsertBefore(filePath, "return services;", $"        services.AddScoped<I{entity.NamePascal}Service, {entity.NamePascal}Manager>();");
    }

    private void GenerateWebApiController(ProjectLayout layout, EntityDefinition entity)
    {
        string controllersDir = Path.Combine(layout.WebApiPath, "Controllers");
        Directory.CreateDirectory(controllersDir);
        string filePath = Path.Combine(controllersDir, $"{entity.PluralPascal}Controller.cs");
        if (File.Exists(filePath))
            return;

        StringBuilder sb = new();
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine("using Core.Application.Requests;");
        sb.AppendLine("using Core.Application.Responses;");
        sb.AppendLine($"using {layout.ProjectName}.Application.Features.{entity.PluralPascal}.Commands.Create;");
        sb.AppendLine($"using {layout.ProjectName}.Application.Features.{entity.PluralPascal}.Commands.Update;");
        sb.AppendLine($"using {layout.ProjectName}.Application.Features.{entity.PluralPascal}.Commands.Delete;");
        sb.AppendLine($"using {layout.ProjectName}.Application.Features.{entity.PluralPascal}.Queries.GetById;");
        sb.AppendLine($"using {layout.ProjectName}.Application.Features.{entity.PluralPascal}.Queries.GetList;");
        sb.AppendLine();
        sb.AppendLine($"namespace {layout.ProjectName}.WebAPI.Controllers;");
        sb.AppendLine();
        sb.AppendLine("[Route(\"api/[controller]\")]");
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"public class {entity.PluralPascal}Controller : BaseController");
        sb.AppendLine("{");
        sb.AppendLine();
        sb.AppendLine("    [HttpPost]");
        sb.AppendLine($"    public async Task<ActionResult<Created{entity.NamePascal}Response>> Create(Create{entity.NamePascal}Command command)");
        sb.AppendLine("    {");
        sb.AppendLine("        var response = await Mediator.Send(command);");
        sb.AppendLine("        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [HttpPut]");
        sb.AppendLine($"    public async Task<ActionResult<Updated{entity.NamePascal}Response>> Update(Update{entity.NamePascal}Command command)");
        sb.AppendLine("    {");
        sb.AppendLine("        var response = await Mediator.Send(command);");
        sb.AppendLine("        return Ok(response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [HttpDelete(\"{id}\")]");
        sb.AppendLine($"    public async Task<ActionResult<Deleted{entity.NamePascal}Response>> Delete({entity.IdType} id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var response = await Mediator.Send(new Delete{entity.NamePascal}Command {{ Id = id }});");
        sb.AppendLine("        return Ok(response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [HttpGet(\"{id}\")]");
        sb.AppendLine($"    public async Task<ActionResult<GetById{entity.NamePascal}Response>> GetById({entity.IdType} id)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var response = await Mediator.Send(new GetById{entity.NamePascal}Query {{ Id = id }});");
        sb.AppendLine("        return Ok(response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [HttpGet]");
        sb.AppendLine($"    public async Task<ActionResult<GetListResponse<GetList{entity.NamePascal}ListItemDto>>> GetList([FromQuery] PageRequest pageRequest)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var query = new GetList{entity.NamePascal}Query {{ PageRequest = pageRequest }};");
        sb.AppendLine("        var response = await Mediator.Send(query);");
        sb.AppendLine("        return Ok(response);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        File.WriteAllText(filePath, sb.ToString());
    }

    private static string BuildResponseClass(string projectName, EntityDefinition entity, string namespaceSuffix, string className)
    {
        StringBuilder sb = new();
        sb.AppendLine("using Core.Application.Responses;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.{namespaceSuffix};");
        sb.AppendLine();
        sb.AppendLine($"public class {className} : IResponse");
        sb.AppendLine("{");
        sb.AppendLine($"    public {entity.IdType} Id {{ get; set; }}");
        foreach (PropertyDefinition property in entity.Properties)
            sb.AppendLine(FormatProperty(property));
        sb.AppendLine();
        sb.AppendLine($"    public {className}()");
        sb.AppendLine("    {");
        // Initialize all string properties with string.Empty for safety
        foreach (PropertyDefinition property in entity.Properties)
        {
            if (property.NonNullableType.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"        {property.PropertyName} = string.Empty;");
            }
        }
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public {className}({entity.IdType} id{BuildConstructorParameters(entity)})");
        sb.AppendLine("    {");
        sb.AppendLine("        Id = id;");
        foreach (PropertyDefinition property in entity.Properties)
            sb.AppendLine($"        {property.PropertyName} = {property.FieldName};");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildDeletedResponseClass(string projectName, EntityDefinition entity)
    {
        StringBuilder sb = new();
        sb.AppendLine("using Core.Application.Responses;");
        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Application.Features.{entity.PluralPascal}.Commands.Delete;");
        sb.AppendLine();
        sb.AppendLine($"public class Deleted{entity.NamePascal}Response : IResponse");
        sb.AppendLine("{");
        sb.AppendLine($"    public {entity.IdType} Id {{ get; set; }}");
        sb.AppendLine();
        sb.AppendLine($"    public Deleted{entity.NamePascal}Response()");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public Deleted{entity.NamePascal}Response({entity.IdType} id)");
        sb.AppendLine("    {");
        sb.AppendLine("        Id = id;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void WriteFileIfNeeded(string filePath, string content, Func<string, bool> shouldOverwrite)
    {
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, content);
            return;
        }

        string existing = File.ReadAllText(filePath);
        if (existing.Equals(content, StringComparison.Ordinal))
            return;

        if (shouldOverwrite(existing))
            File.WriteAllText(filePath, content);
    }

    private static void RemoveLegacyServiceFolderIfObsolete(string legacyServicesRoot, string servicesRoot, EntityDefinition entity)
    {
        if (!Directory.Exists(legacyServicesRoot))
            return;

        string[] entries = Directory.GetFileSystemEntries(legacyServicesRoot);
        if (entries.Length == 0)
        {
            Directory.Delete(legacyServicesRoot, true);
            return;
        }

        string interfaceFile = Path.Combine(legacyServicesRoot, $"I{entity.NamePascal}Service.cs");
        string managerFile = Path.Combine(legacyServicesRoot, $"{entity.NamePascal}Manager.cs");

        bool ContainsLegacyContent(string path)
        {
            if (!File.Exists(path))
                return false;
            string text = File.ReadAllText(path);
            return text.Contains("global::", StringComparison.Ordinal)
                   || text.Contains($"namespace ", StringComparison.Ordinal) && text.Contains($"Application.Services.{entity.PluralPascal};", StringComparison.Ordinal);
        }

        bool onlyKnownFiles = entries.All(entry =>
        {
            string fileName = Path.GetFileName(entry);
            return string.Equals(fileName, Path.GetFileName(interfaceFile), StringComparison.Ordinal)
                   || string.Equals(fileName, Path.GetFileName(managerFile), StringComparison.Ordinal);
        });

        if (!onlyKnownFiles)
            return;

        if (ContainsLegacyContent(interfaceFile) || ContainsLegacyContent(managerFile))
        {
            if (Directory.Exists(servicesRoot))
                Directory.Delete(legacyServicesRoot, true);
        }
    }

    private static string FormatProperty(PropertyDefinition property)
    {
        // Never use property initializers, always use constructor initialization
        return $"    public {property.Type} {property.PropertyName} {{ get; set; }}";
    }

    private static string BuildConstructorParameters(EntityDefinition entity)
    {
        if (entity.Properties.Count == 0)
            return string.Empty;

        return string.Concat(entity.Properties.Select(p => $", {p.Type} {p.FieldName}"));
    }

    private void UpdateOperationClaimConfiguration(ProjectLayout layout, EntityDefinition entity)
    {
        string persistencePath = layout.PersistencePath;
        string operationClaimConfigPath = Path.Combine(persistencePath, "EntityConfigurations", "OperationClaimConfiguration.cs");

        if (!File.Exists(operationClaimConfigPath))
        {
            Console.WriteLine($"⚠️  OperationClaimConfiguration.cs not found at: {operationClaimConfigPath}");
            return;
        }

        string content = File.ReadAllText(operationClaimConfigPath);

        // Check if this entity's operation claims already exist
        string entityRegionComment = $"region {entity.PluralPascal}";
        if (content.Contains(entityRegionComment))
        {
            Console.WriteLine($"✅ Operation claims for {entity.PluralPascal} already exist in OperationClaimConfiguration.cs");
            return;
        }

        // Add using statement for the entity's operation claims
        string usingStatement = $"using {layout.ProjectName}.Application.Features.{entity.PluralPascal}.Constants;";
        if (!content.Contains(usingStatement))
        {
            // Find the last using statement and add our using after it
            int lastUsingIndex = content.LastIndexOf("using ");
            if (lastUsingIndex != -1)
            {
                int lineEndIndex = content.IndexOf('\n', lastUsingIndex);
                if (lineEndIndex != -1)
                {
                    content = content.Insert(lineEndIndex + 1, usingStatement + "\n");
                }
            }
        }

        // Find the insertion point (before the last return statement in getFeatureOperationClaims method)
        string returnStatement = "return featureOperationClaims;";
        int returnIndex = content.LastIndexOf(returnStatement);

        if (returnIndex == -1)
        {
            Console.WriteLine("⚠️  Could not find the return statement in getFeatureOperationClaims method");
            return;
        }

        // Create the new operation claims section
        StringBuilder newSection = new();
        newSection.AppendLine();
        newSection.AppendLine($"        #{entityRegionComment}");
        newSection.AppendLine("        featureOperationClaims.AddRange(");
        newSection.AppendLine("            [");
        newSection.AppendLine($"                new() {{ Id = ++lastId, Name = {entity.PluralPascal}OperationClaims.Admin }},");
        newSection.AppendLine($"                new() {{ Id = ++lastId, Name = {entity.PluralPascal}OperationClaims.Read }},");
        newSection.AppendLine($"                new() {{ Id = ++lastId, Name = {entity.PluralPascal}OperationClaims.Write }},");
        newSection.AppendLine($"                new() {{ Id = ++lastId, Name = {entity.PluralPascal}OperationClaims.Create }},");
        newSection.AppendLine($"                new() {{ Id = ++lastId, Name = {entity.PluralPascal}OperationClaims.Update }},");
        newSection.AppendLine($"                new() {{ Id = ++lastId, Name = {entity.PluralPascal}OperationClaims.Delete }},");
        newSection.AppendLine("            ]");
        newSection.AppendLine("        );");
        newSection.AppendLine("        #endregion");
        newSection.AppendLine();

        // Insert the new section before the return statement
        content = content.Insert(returnIndex, newSection.ToString());

        // Write the updated content back to the file
        File.WriteAllText(operationClaimConfigPath, content);

        Console.WriteLine($"✅ Added operation claims for {entity.PluralPascal} to OperationClaimConfiguration.cs");
    }
}
