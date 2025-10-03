using Archigen.Core;
using Archigen.Generator;

using System.Text;
using System.Text.RegularExpressions;

// Ensure UTF-8 so box-drawing characters render correctly
try { Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); } catch { }

string[] arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();

// If no arguments, launch interactive menu by default
if (arguments.Length == 0)
{
    RunInteractiveMenu();
    return;
}

if (arguments[0] is "-h" or "--help")
{
    PrintHelp();
    return;
}

switch (arguments[0])
{
    case "menu":
        RunInteractiveMenu();
        break;
    case "new":
        RunNewProject(arguments);
        break;
    case "layout-test":
        RunLayoutTest(arguments);
        break;
    case "parse-entity":
        RunParseEntity(arguments);
        break;
    case "crud":
        RunCrud(arguments);
        break;
    default:
        Console.Error.WriteLine($"Unknown command '{arguments[0]}'.");
        PrintHelp();
        break;
}

static void RunInteractiveMenu()
{
    while (true)
    {
        try { Console.Clear(); } catch { /* Some hosts may not support clear; ignore */ }
        RenderMenuUI();
        Console.Write("Select an option: ");

        string? input = Console.ReadLine()?.Trim();
        switch (input)
        {
            case "1":
                RunGuidedCrud();
                break;
            case "2":
                RunAddPropertyInteractive();
                break;
            case "3":
                RunNewProjectInteractive();
                break;
            case "4":
                RunMigrationInteractive();
                break;
            case "5":
                RunProjectInteractive();
                break;
            case "0":
                return;
            default:
                Console.WriteLine("Invalid selection. Please choose 0, 1, 2, 3, 4, or 5.");
                break;
        }
    }
}

static void RenderMenuUI()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Archigen CLI");
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("Fast .NET Clean Architecture scaffolding");
    Console.ResetColor();
    Console.WriteLine();

    Console.WriteLine("1) Generate CRUD for an existing solution (guided)");
    Console.WriteLine("2) Add new property to an existing domain");
    Console.WriteLine("3) Generate new project");
    Console.WriteLine("4) Run database migration");
    Console.WriteLine("5) Run project (WebAPI)");
    Console.WriteLine("0) Exit");
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("Tips:");
    Console.WriteLine(" - Press 1/2/3/4/0 and Enter");
    Console.WriteLine(" - Run 'archigen --help' for CLI usage");
    Console.ResetColor();
    Console.WriteLine();
}

static void RunGuidedCrud()
{
    // Step 1: Prompt for solution root directory with current directory as default
    string defaultRoot = Directory.GetCurrentDirectory();
    Console.Write($"Solution root [default: {defaultRoot}]: ");
    string? solutionRoot = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(solutionRoot))
        solutionRoot = defaultRoot;

    if (!Directory.Exists(solutionRoot))
    {
        Console.WriteLine($"Directory not found: {solutionRoot}");
        return;
    }

    // Step 2: Detect existing projects by scanning for Clean Architecture layer folders
    string projectParent = Path.Combine(solutionRoot, "project");
    if (!Directory.Exists(projectParent))
    {
        Console.WriteLine($"Could not find 'project' folder under: {solutionRoot}");
        return;
    }

    string[] domainDirs = Directory.GetDirectories(projectParent, "*.Domain", SearchOption.TopDirectoryOnly);
    if (domainDirs.Length == 0)
    {
        Console.WriteLine("No Domain projects found under 'project'.");
        return;
    }

    var projectNames = domainDirs
        .Select(Path.GetFileName)
        .Where(n => n != null)
        .Select(n => n!.Replace(".Domain", string.Empty))
        .OrderBy(n => n)
        .ToList();

    Console.WriteLine();
    Console.WriteLine("Available projects:");
    for (int i = 0; i < projectNames.Count; i++)
        Console.WriteLine($"  {i + 1}) {projectNames[i]}");
    Console.Write("Select project by number: ");
    int projectIndex = ReadIndex(1, projectNames.Count);
    if (projectIndex == -1) return;
    string projectName = projectNames[projectIndex - 1];

    // Step 3: List existing domain entities and allow creation of new ones
    string entitiesDir = Path.Combine(projectParent, $"{projectName}.Domain", "Entities");
    Directory.CreateDirectory(entitiesDir);
    var entityFiles = Directory.GetFiles(entitiesDir, "*.cs");
    List<string> existingEntities = entityFiles
        .Select(Path.GetFileNameWithoutExtension)
        .Where(n => !string.IsNullOrEmpty(n))
        .Select(n => n!)
        .OrderBy(n => n)
        .ToList();

    Console.WriteLine();
    Console.WriteLine("Entities:");
    for (int i = 0; i < existingEntities.Count; i++)
        Console.WriteLine($"  {i + 1}) {existingEntities[i]}");
    Console.WriteLine($"  N) New entity");
    Console.Write("Select entity by number or 'N' for new: ");
    string? entitySelection = Console.ReadLine()?.Trim();

    string entityName;
    IList<PropertyDefinition> props = new List<PropertyDefinition>();
    if (!string.IsNullOrEmpty(entitySelection) && (entitySelection.Equals("N", StringComparison.OrdinalIgnoreCase)))
    {
        while (true)
        {
            Console.Write("Enter new entity name: ");
            entityName = (Console.ReadLine() ?? "").Trim();

            if (string.IsNullOrWhiteSpace(entityName))
            {
                Console.WriteLine("❌ Entity name is required.");
                continue;
            }

            // Validate the entered entity name for compliance and conflicts
            try
            {
                ValidateEntityName(entityName, existingEntities);
                break; // Entity name is valid, proceed with generation
            }
            catch (ArgumentException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ {ex.Message}");
                Console.ResetColor();
                continue;
            }
        }

        Console.WriteLine("Enter properties (format: Name:string,Count:int?,CreatedAt:DateTime)");
        while (true)
        {
            Console.Write("Properties (optional): ");
            string? propsArg = Console.ReadLine();

            try
            {
                props = ParseProperties(propsArg, entityName);
                break;
            }
            catch (ArgumentException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("Please try again with different property names.");
            }
        }
    }
    else
    {
        if (!int.TryParse(entitySelection, out int idx) || idx < 1 || idx > existingEntities.Count)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }
        entityName = existingEntities[idx - 1];
    }

    // Step 4: Configure DbContext and primary key type with sensible defaults
    Console.Write("DbContext name [default: BaseDbContext]: ");
    string? dbContextName = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(dbContextName)) dbContextName = "BaseDbContext";

    Console.Write("Id type [default: int]: ");
    string? idType = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(idType)) idType = "int";

    // Step 5: Configure security and authorization requirements
    Console.Write("Enable authorization for CRUD operations? [Y/n]: ");
    string? securityChoice = Console.ReadLine()?.Trim();
    bool enableSecurity = string.IsNullOrEmpty(securityChoice) || securityChoice.Equals("y", StringComparison.OrdinalIgnoreCase);

    // Step 6: Execute CRUD code generation with configured parameters
    try
    {
        ProjectLayout layout = ProjectLayout.Create(solutionRoot!, projectName);
        CrudGenerator generator = new();
        generator.Generate(layout, new CrudGenerationOptions
        {
            ProjectName = projectName,
            EntityName = entityName,
            DbContextName = dbContextName!,
            IdType = idType!,
            Properties = props,
            EnableSecurity = enableSecurity
        });
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✅ CRUD scaffolding for '{entityName}' has been generated successfully!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("🎉 Generated components:");
        Console.WriteLine($"   • Domain Entity: {entityName}.cs");
        Console.WriteLine($"   • Commands: Create{entityName}Command, Update{entityName}Command, Delete{entityName}Command");
        Console.WriteLine($"   • Queries: GetById{entityName}Query, GetList{entityName}Query");
        Console.WriteLine($"   • Responses & DTOs");
        Console.WriteLine($"   • Business Rules & Mapping Profiles");
        Console.WriteLine($"   • WebAPI Controller: {entityName}Controller");
        if (enableSecurity)
        {
            Console.WriteLine($"   • {entityName}OperationClaims added to OperationClaimConfiguration");
        }
        Console.WriteLine();
        Console.WriteLine("🚀 Next steps:");
        Console.WriteLine("   1. Create and run a database migration");
        Console.WriteLine("   2. Build and test your application");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Failed to generate CRUD: {ex.Message}");
        Console.ResetColor();
    }

    Console.WriteLine("\nPress any key to return to main menu...");
    Console.ReadKey();
}

static void RunNewProjectInteractive()
{
    Console.WriteLine();
    Console.WriteLine("New Project Wizard");
    Console.WriteLine("-------------------");

    // Destination parent
    string defaultDest = Directory.GetCurrentDirectory();
    Console.Write($"Destination parent directory [default: {defaultDest}]: ");
    string? destParent = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(destParent)) destParent = defaultDest;
    if (!Directory.Exists(destParent))
    {
        Console.WriteLine($"Directory not found: {destParent}");
        return;
    }

    // Project name
    Console.Write("Project name (PascalCase, e.g., MyCompany): ");
    string? projectName = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(projectName))
    {
        Console.WriteLine("Project name is required.");
        return;
    }

    string destRoot = Path.Combine(destParent!, projectName);
    if (Directory.Exists(destRoot) && Directory.EnumerateFileSystemEntries(destRoot).Any())
    {
        Console.Write($"Directory '{destRoot}' already exists and is not empty. Overwrite? [y/N]: ");
        string? ans = Console.ReadLine();
        if (!string.Equals(ans, "y", StringComparison.OrdinalIgnoreCase))
            return;
    }

    try
    {
        GenerateProjectFromTemplate(projectName!, destRoot, overwrite: true);

        // Change to the new project directory
        Directory.SetCurrentDirectory(destRoot);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ Project created successfully!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"📁 Project location: {destRoot}");
        Console.WriteLine($"📂 Current directory changed to: {destRoot}");
        Console.WriteLine();

        // Run dotnet restore
        Console.WriteLine("📦 Restoring NuGet packages...");
        var restoreResult = RunDotnetCommand(destRoot, "dotnet restore");

        if (restoreResult.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ NuGet packages restored successfully!");
            Console.ResetColor();
            Console.WriteLine();

            // Run dotnet build
            Console.WriteLine("🔨 Building solution...");
            var buildResult = RunDotnetCommand(destRoot, $"dotnet build {projectName}.sln");

            if (buildResult.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Solution built successfully!");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("🎉 Project is ready to use!");
                Console.WriteLine();
                Console.WriteLine("🚀 Next steps:");
                Console.WriteLine($"   archigen menu  (to add entities)");
                Console.WriteLine($"   dotnet run --project project/{projectName}.WebAPI  (to start the API)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  Build completed with warnings or errors.");
                Console.ResetColor();
                Console.WriteLine("You may need to fix any issues before running the application.");
                Console.WriteLine();
                Console.WriteLine("🚀 Next steps:");
                Console.WriteLine($"   dotnet build {projectName}.sln  (to retry build)");
                Console.WriteLine($"   archigen menu  (to add entities)");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Package restore failed. You may need to restore manually.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("🚀 Next steps:");
            Console.WriteLine($"   dotnet restore  (to retry restore)");
            Console.WriteLine($"   dotnet build {projectName}.sln  (after successful restore)");
            Console.WriteLine($"   archigen menu  (to add entities)");
        }
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("💡 Tip: You're now in the project directory. Press anything to open the menu to add entities!");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Error: {ex.Message}");
        Console.ResetColor();
    }

    Console.WriteLine("\nPress any key to return to main menu...");
    Console.ReadKey();
}

static void RunNewProject(string[] args)
{
    // Usage: archigen new <ProjectName> [--output path] [--force]
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: archigen new <ProjectName> [--output path] [--force]");
        Console.WriteLine("No project name supplied. Launching interactive wizard...\n");
        RunNewProjectInteractive();
        return;
    }

    string projectName = args[1];
    string destParent = Directory.GetCurrentDirectory();
    bool force = false;

    for (int i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--output":
                destParent = GetOptionValue(args, ref i);
                break;
            case "--force":
                force = true;
                break;
            default:
                throw new ArgumentException($"Unknown option '{args[i]}'.");
        }
    }

    string destRoot = Path.Combine(destParent, projectName);
    if (!force && Directory.Exists(destRoot) && Directory.EnumerateFileSystemEntries(destRoot).Any())
        throw new IOException($"Destination '{destRoot}' already exists and is not empty. Use --force to overwrite.");

    GenerateProjectFromTemplate(projectName, destRoot, overwrite: force);

    // Change to the new project directory
    Directory.SetCurrentDirectory(destRoot);

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅ Project created successfully!");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine($"📁 Project location: {destRoot}");
    Console.WriteLine($"📂 Current directory changed to: {destRoot}");
    Console.WriteLine();

    // Run dotnet restore
    Console.WriteLine("📦 Restoring NuGet packages...");
    var restoreResult = RunDotnetCommand(destRoot, "dotnet restore");

    if (restoreResult.Success)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ NuGet packages restored successfully!");
        Console.ResetColor();
        Console.WriteLine();

        // Run dotnet build
        Console.WriteLine("� Building solution...");
        var buildResult = RunDotnetCommand(destRoot, $"dotnet build {projectName}.sln");

        if (buildResult.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Solution built successfully!");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("🎉 Project is ready to use!");
            Console.WriteLine();
            Console.WriteLine("�🚀 You can now:");
            Console.WriteLine($"   archigen menu  (to add entities)");
            Console.WriteLine($"   dotnet run --project project/{projectName}.WebAPI  (to start the API)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Build completed with warnings or errors.");
            Console.ResetColor();
            Console.WriteLine("You may need to fix any issues before running the application.");
            Console.WriteLine();
            Console.WriteLine("🚀 Next steps:");
            Console.WriteLine($"   dotnet build {projectName}.sln  (to retry build)");
            Console.WriteLine($"   archigen menu  (to add entities)");
        }
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  Package restore failed. You may need to restore manually.");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("🚀 Next steps:");
        Console.WriteLine($"   dotnet restore  (to retry restore)");
        Console.WriteLine($"   dotnet build {projectName}.sln  (after successful restore)");
        Console.WriteLine($"   archigen menu  (to add entities)");
    }
}

static void GenerateProjectFromTemplate(string projectName, string destinationRoot, bool overwrite)
{
    string? templateRoot = FindTemplateRoot();
    if (templateRoot is null)
        throw new DirectoryNotFoundException("Could not locate 'template' folder. Ensure it is bundled with the CLI or available in the repository.");

    if (Directory.Exists(destinationRoot))
    {
        if (!overwrite)
            throw new IOException($"Destination '{destinationRoot}' already exists. Use overwrite=true to replace.");
    }
    Directory.CreateDirectory(destinationRoot);

    // Pre-generate new GUIDs for .sln file replacement
    var guidMap = new Dictionary<string, string>();

    // Copy and transform
    var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".sln", ".csproj", ".cs", ".json", ".md", ".yml", ".yaml", ".editorconfig", ".gitattributes", ".gitignore", ".txt", ".props", ".targets", ".http"
    };

    foreach (string srcDir in Directory.GetDirectories(templateRoot, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(templateRoot, srcDir);
        string transformed = relative.Replace("Project", projectName);
        string destDir = Path.Combine(destinationRoot, transformed);
        Directory.CreateDirectory(destDir);
    }

    foreach (string srcFile in Directory.GetFiles(templateRoot, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(templateRoot, srcFile);
        string fileNameTransformed = Path.GetFileName(relative).Replace("template.sln", projectName + ".sln").Replace("Project", projectName);
        string dirPart = Path.GetDirectoryName(relative) ?? string.Empty;
        string destPath = Path.Combine(destinationRoot, dirPart.Replace("Project", projectName), fileNameTransformed);

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        string ext = Path.GetExtension(srcFile);
        if (textExtensions.Contains(ext))
        {
            string content = File.ReadAllText(srcFile, Encoding.UTF8);

            // For .sln files, use careful replacement to avoid breaking keywords
            if (ext.Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                // Only replace "Project" in project names and paths, not .sln keywords
                content = Regex.Replace(content, @"\bProject\.Application\b", $"{projectName}.Application", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Domain\b", $"{projectName}.Domain", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Infrastructure\b", $"{projectName}.Infrastructure", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Persistence\b", $"{projectName}.Persistence", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.WebAPI\b", $"{projectName}.WebAPI", RegexOptions.IgnoreCase);

                // Replace in file paths: project/Project.* -> project/ProjectName.*
                content = content.Replace("project\\Project.Application\\", $"project\\{projectName}.Application\\");
                content = content.Replace("project\\Project.Domain\\", $"project\\{projectName}.Domain\\");
                content = content.Replace("project\\Project.Infrastructure\\", $"project\\{projectName}.Infrastructure\\");
                content = content.Replace("project\\Project.Persistence\\", $"project\\{projectName}.Persistence\\");
                content = content.Replace("project\\Project.WebAPI\\", $"project\\{projectName}.WebAPI\\");

                // And Unix paths too
                content = content.Replace("project/Project.Application/", $"project/{projectName}.Application/");
                content = content.Replace("project/Project.Domain/", $"project/{projectName}.Domain/");
                content = content.Replace("project/Project.Infrastructure/", $"project/{projectName}.Infrastructure/");
                content = content.Replace("project/Project.Persistence/", $"project/{projectName}.Persistence/");
                content = content.Replace("project/Project.WebAPI/", $"project/{projectName}.WebAPI/");

                content = TransformSolutionFile(content, guidMap);
            }
            else if (ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                // For .csproj files, be careful not to replace XML elements
                content = Regex.Replace(content, @"\bProject\.Application\b", $"{projectName}.Application", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Domain\b", $"{projectName}.Domain", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Infrastructure\b", $"{projectName}.Infrastructure", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Persistence\b", $"{projectName}.Persistence", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.WebAPI\b", $"{projectName}.WebAPI", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Core\b", $"{projectName}.Core", RegexOptions.IgnoreCase);

                // Replace in assembly names and namespaces, but not in XML element names
                content = Regex.Replace(content, @"<AssemblyName>Project\b", $"<AssemblyName>{projectName}", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"<RootNamespace>Project\b", $"<RootNamespace>{projectName}", RegexOptions.IgnoreCase);
            }
            else if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                // For JSON files, be careful not to replace commandName or other special values
                // Only replace Project in namespace-like contexts, not in configuration values
                content = Regex.Replace(content, @"\bProject\.Application\b", $"{projectName}.Application", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Domain\b", $"{projectName}.Domain", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Infrastructure\b", $"{projectName}.Infrastructure", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Persistence\b", $"{projectName}.Persistence", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.WebAPI\b", $"{projectName}.WebAPI", RegexOptions.IgnoreCase);
                content = Regex.Replace(content, @"\bProject\.Core\b", $"{projectName}.Core", RegexOptions.IgnoreCase);
                // Don't do global Project replacement for JSON files to preserve commandName: "Project"
            }
            else
            {
                // For other text files, do global replacement
                content = content.Replace("Project", projectName);
            }

            File.WriteAllText(destPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        else
        {
            File.Copy(srcFile, destPath, overwrite: true);
        }
    }
}

static string TransformSolutionFile(string slnContent, Dictionary<string, string> guidMap)
{
    // Don't replace the standard solution folder type GUID
    const string solutionFolderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

    // Replace GUIDs with new ones to avoid conflicts, but preserve solution folder type GUID
    var guidRegex = new Regex(@"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}");

    string result = guidRegex.Replace(slnContent, match =>
    {
        string oldGuid = match.Value;

        // Never replace the standard solution folder type GUID
        if (string.Equals(oldGuid, solutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase))
        {
            return oldGuid;
        }

        if (!guidMap.ContainsKey(oldGuid))
        {
            guidMap[oldGuid] = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
        }
        return guidMap[oldGuid];
    });

    // Normalize path separators for current platform
    if (Path.DirectorySeparatorChar == '/')
    {
        result = result.Replace('\\', '/');
    }

    return result;
}

static string? FindTemplateRoot()
{
    // 1) Prefer bundled 'template' folder next to the executable
    string baseDir = AppContext.BaseDirectory;
    string candidate = Path.Combine(baseDir, "template");
    if (Directory.Exists(candidate)) return candidate;

    // 2) Search upwards for a folder named 'template' (useful when running from source)
    try
    {
        string? dir = baseDir;
        for (int i = 0; i < 6 && dir != null; i++)
        {
            string probe = Path.Combine(dir, "template");
            if (Directory.Exists(probe)) return probe;
            dir = Directory.GetParent(dir)?.FullName;
        }
    }
    catch { /* ignored */ }

    // 3) Try current working directory
    string cwdProbe = Path.Combine(Directory.GetCurrentDirectory(), "template");
    if (Directory.Exists(cwdProbe)) return cwdProbe;

    return null;
}

static int ReadIndex(int minInclusive, int maxInclusive)
{
    string? input = Console.ReadLine();
    if (!int.TryParse(input, out int choice))
    {
        Console.WriteLine("Input is not a number.");
        return -1;
    }
    if (choice < minInclusive || choice > maxInclusive)
    {
        Console.WriteLine($"Please enter a number between {minInclusive} and {maxInclusive}.");
        return -1;
    }
    return choice;
}

static void RunLayoutTest(string[] commandArgs)
{
    if (commandArgs.Length < 3)
    {
        Console.Error.WriteLine("Usage: archigen layout-test <solutionRoot> <projectName>");
        return;
    }

    ProjectLayout layout = ProjectLayout.Create(commandArgs[1], commandArgs[2]);
    Console.WriteLine($"Application: {layout.ApplicationPath}");
    Console.WriteLine($"Domain: {layout.DomainPath}");
    Console.WriteLine($"Persistence: {layout.PersistencePath}");
    Console.WriteLine($"WebAPI: {layout.WebApiPath}");
}

static void RunParseEntity(string[] commandArgs)
{
    if (commandArgs.Length < 2)
    {
        Console.Error.WriteLine("Usage: archigen parse-entity <entityFile>");
        return;
    }

    EntityDefinition entity = EntityParser.Parse(commandArgs[1]);
    Console.WriteLine($"Entity: {entity.NamePascal} (Id type: {entity.IdType})");
    foreach (PropertyDefinition property in entity.Properties)
        Console.WriteLine($" - {property.PropertyName}: {property.Type}");
}

static void RunCrud(string[] commandArgs)
{
    if (commandArgs.Length < 3)
    {
        Console.Error.WriteLine("Usage: archigen crud <ProjectName> <EntityName> [--solution path] [--props Name:string,Description:string?] [--dbcontext BaseDbContext] [--id int]");
        return;
    }

    string projectName = commandArgs[1];
    string entityName = commandArgs[2];
    string solutionRoot = Directory.GetCurrentDirectory();
    string dbContextName = "BaseDbContext";
    string idType = "int";
    string? propsArgument = null;

    for (int i = 3; i < commandArgs.Length; i++)
    {
        switch (commandArgs[i])
        {
            case "--solution":
                solutionRoot = GetOptionValue(commandArgs, ref i);
                break;
            case "--props":
                propsArgument = GetOptionValue(commandArgs, ref i);
                break;
            case "--dbcontext":
                dbContextName = GetOptionValue(commandArgs, ref i);
                break;
            case "--id":
                idType = GetOptionValue(commandArgs, ref i);
                break;
            default:
                throw new ArgumentException($"Unknown option '{commandArgs[i]}'.");
        }
    }

    IList<PropertyDefinition> properties = ParseProperties(propsArgument, entityName);

    ProjectLayout layout = ProjectLayout.Create(solutionRoot, projectName);
    CrudGenerator generator = new();
    generator.Generate(layout, new CrudGenerationOptions
    {
        ProjectName = projectName,
        EntityName = entityName,
        DbContextName = dbContextName,
        IdType = idType,
        Properties = properties
    });

    Console.WriteLine($"CRUD scaffolding for '{entityName}' has been generated.");
}

static string GetOptionValue(string[] args, ref int index)
{
    if (index + 1 >= args.Length)
        throw new ArgumentException($"Option '{args[index]}' requires a value.");
    return args[++index];
}

static IList<PropertyDefinition> ParseProperties(string? value, string? entityName = null)
{
    List<PropertyDefinition> properties = new();
    if (string.IsNullOrWhiteSpace(value))
        return properties;

    string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (string part in parts)
    {
        string[] segments = part.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string name = segments[0];
        string type = segments.Length > 1 ? segments[1] : "string";

        // Comprehensive validation
        ValidatePropertyName(name, entityName);
        ValidatePropertyType(type);

        properties.Add(new PropertyDefinition
        {
            Name = name,
            Type = type
        });
    }

    return properties;
}

static void RunMigrationInteractive()
{
    Console.WriteLine();
    Console.WriteLine("Database Migration");
    Console.WriteLine("------------------");

    // Check if we're in a valid .NET project directory
    string currentDir = Directory.GetCurrentDirectory();

    // Look for Persistence and WebAPI projects
    var persistenceFiles = Directory.GetFiles(currentDir, "*.csproj", SearchOption.AllDirectories)
        .Where(f => Path.GetFileName(f).Contains(".Persistence") || Path.GetFileName(f).Contains("Persistence"))
        .ToArray();

    var webApiFiles = Directory.GetFiles(currentDir, "*.csproj", SearchOption.AllDirectories)
        .Where(f => Path.GetFileName(f).Contains(".WebAPI") || Path.GetFileName(f).Contains("WebAPI"))
        .ToArray();

    if (persistenceFiles.Length == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ No Persistence project found in current directory or subdirectories.");
        Console.ResetColor();
        Console.WriteLine("Please run this command from your solution root directory.");
        return;
    }

    if (webApiFiles.Length == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ No WebAPI project found in current directory or subdirectories.");
        Console.ResetColor();
        Console.WriteLine("Please run this command from your solution root directory.");
        return;
    }

    string persistenceProject = persistenceFiles[0];
    string webApiProject = webApiFiles[0];
    string persistenceDir = Path.GetDirectoryName(persistenceProject)!;

    Console.WriteLine($"📁 Found Persistence project: {Path.GetFileName(persistenceProject)}");
    Console.WriteLine($"📁 Found WebAPI project: {Path.GetFileName(webApiProject)}");
    Console.WriteLine();

    // First, check for DbContexts and let user select if multiple found
    var dbContexts = GetDbContexts(persistenceProject);

    if (dbContexts.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ No DbContext classes found in the project.");
        Console.ResetColor();
        return;
    }

    string? selectedDbContext = null;

    if (dbContexts.Count > 1)
    {
        Console.WriteLine("🔍 Multiple DbContext classes found:");
        for (int i = 0; i < dbContexts.Count; i++)
        {
            Console.WriteLine($"{i + 1}) {dbContexts[i]}");
        }

        Console.WriteLine();
        Console.Write($"Which DbContext do you want to use for migration? (1-{dbContexts.Count}): ");

        string? choice = Console.ReadLine()?.Trim();
        if (int.TryParse(choice, out int selectedIndex) && selectedIndex >= 1 && selectedIndex <= dbContexts.Count)
        {
            selectedDbContext = dbContexts[selectedIndex - 1];
            Console.WriteLine($"🎯 Selected DbContext: {selectedDbContext}");
            Console.WriteLine();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Invalid selection.");
            Console.ResetColor();
            return;
        }
    }
    else
    {
        selectedDbContext = dbContexts[0];
        Console.WriteLine($"📊 Using DbContext: {selectedDbContext}");
        Console.WriteLine();
    }

    // Now get migration name
    Console.Write("Migration name (e.g., AddCarEntity): ");
    string? migrationName = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(migrationName))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ Migration name is required.");
        Console.ResetColor();
        return;
    }

    // Run dotnet ef migrations add
    Console.WriteLine();
    Console.WriteLine("🚀 Creating migration...");

    try
    {
        string migrationCommand = $"dotnet ef migrations add {migrationName} --context {selectedDbContext} --project {persistenceProject} --startup-project {webApiProject}";
        var addResult = RunDotnetCommand(currentDir, migrationCommand);

        if (addResult.Success && addResult.Error.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Migration '{migrationName}' created successfully!");
            Console.ResetColor();
            Console.WriteLine();

            // Ask if user wants to update database
            Console.Write("Update database now? [Y/n]: ");
            string? updateChoice = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(updateChoice) || updateChoice.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("🔄 Updating database...");

                try
                {
                    var updateResult = RunDotnetCommand(currentDir, $"dotnet ef database update --context {selectedDbContext} --project {persistenceProject} --startup-project {webApiProject}");

                    if (updateResult.Success && updateResult.Error.Length == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✅ Database updated successfully!");
                        Console.ResetColor();

                        Console.WriteLine();
                        Console.WriteLine("Press any key to return to main menu...");
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ Database update failed!");
                        Console.ResetColor();

                        string errorMessage = updateResult.Error.ToLowerInvariant();

                        if (errorMessage.Contains("connection") || errorMessage.Contains("server") ||
                            errorMessage.Contains("timeout") || errorMessage.Contains("network"))
                        {
                            Console.WriteLine();
                            Console.WriteLine("💡 Please check your database connection string in appsettings.json");
                            Console.WriteLine("   Make sure your database server is running and accessible.");
                        }
                        else if (errorMessage.Contains("login") || errorMessage.Contains("authentication") ||
                                errorMessage.Contains("password") || errorMessage.Contains("user"))
                        {
                            Console.WriteLine();
                            Console.WriteLine("💡 Please check your database credentials in appsettings.json");
                            Console.WriteLine("   Verify username and password are correct.");
                        }
                        else if (errorMessage.Contains("database") && errorMessage.Contains("exist"))
                        {
                            Console.WriteLine();
                            Console.WriteLine("💡 The target database does not exist.");
                            Console.WriteLine("   Please create the database or check the database name in connection string.");
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.WriteLine("💡 Please check your database connection and try again.");
                            if (!string.IsNullOrEmpty(updateResult.Error))
                            {
                                Console.WriteLine($"   Error details: {updateResult.Error.Trim()}");
                            }
                        }

                        Console.WriteLine();
                        Console.WriteLine("1) Try again");
                        Console.WriteLine("0) Return to main menu");
                        Console.Write("Choose an option: ");

                        var retryChoice = Console.ReadLine();
                        if (retryChoice == "1")
                        {
                            RunMigrationInteractive();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Database update failed!");
                    Console.WriteLine($"💡 Please check your database connection: {ex.Message}");
                    Console.ResetColor();

                    Console.WriteLine();
                    Console.WriteLine("1) Try again");
                    Console.WriteLine("0) Return to main menu");
                    Console.Write("Choose an option: ");

                    var retryChoice = Console.ReadLine();
                    if (retryChoice == "1")
                    {
                        RunMigrationInteractive();
                        return;
                    }
                }
            }
            else
            {
                Console.WriteLine("ℹ️  Database update skipped. Run 'dotnet ef database update' manually when ready.");
                Console.WriteLine();
                Console.WriteLine("Press any key to return to main menu...");
                Console.ReadKey();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Migration creation failed!");
            Console.ResetColor();

            string errorMessage = addResult.Error.ToLowerInvariant();

            if (errorMessage.Contains("connection") || errorMessage.Contains("server") ||
                errorMessage.Contains("timeout") || errorMessage.Contains("network"))
            {
                Console.WriteLine();
                Console.WriteLine("💡 Please check your database connection string in appsettings.json");
                Console.WriteLine("   Make sure your database server is running and accessible.");
            }
            else if (errorMessage.Contains("build") || errorMessage.Contains("compile"))
            {
                Console.WriteLine();
                Console.WriteLine("💡 Build failed. Please fix compilation errors first.");
                Console.WriteLine("   Run 'dotnet build' to see detailed build errors.");
            }
            else if (errorMessage.Contains("more than one dbcontext") || errorMessage.Contains("specify which one to use"))
            {
                Console.WriteLine();
                Console.WriteLine("💡 Multiple DbContext detected but not handled properly.");
                Console.WriteLine("   This shouldn't happen with the new flow. Please try again.");
            }
            else if (errorMessage.Contains("dbcontext") || errorMessage.Contains("context"))
            {
                Console.WriteLine();
                Console.WriteLine("💡 DbContext configuration issue.");
                Console.WriteLine("   Please check your DbContext setup in the project.");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("💡 Please check your project configuration and try again.");
                if (!string.IsNullOrEmpty(addResult.Error))
                {
                    Console.WriteLine($"   Error details: {addResult.Error.Trim()}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("1) Try again");
            Console.WriteLine("0) Return to main menu");
            Console.Write("Choose an option: ");

            var retryChoice = Console.ReadLine();
            if (retryChoice == "1")
            {
                RunMigrationInteractive();
                return;
            }
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Error: {ex.Message}");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("1) Try again");
        Console.WriteLine("0) Return to main menu");
        Console.Write("Choose an option: ");

        var retryChoice = Console.ReadLine();
        if (retryChoice == "1")
        {
            RunMigrationInteractive();
            return;
        }
    }
}

static (bool Success, string Output, string Error) RunDotnetCommand(string workingDirectory, string command)
{
    try
    {
        var processInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = command.Substring("dotnet ".Length), // Remove "dotnet " prefix
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(processInfo);
        if (process == null)
            return (false, "", "Failed to start process");

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        // Read output and error streams asynchronously to show real-time output
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine(e.Data);
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Data);
                Console.ResetColor();
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        bool success = process.ExitCode == 0;
        string output = outputBuilder.ToString();
        string error = errorBuilder.ToString();

        return (success, output, error);
    }
    catch (Exception ex)
    {
        return (false, "", ex.Message);
    }
}

static List<string> GetDbContexts(string persistenceProjectPath)
{
    var dbContexts = new List<string>();
    string persistenceDir = Path.GetDirectoryName(persistenceProjectPath)!;

    // Look for DbContext classes in Contexts folder
    string contextsDir = Path.Combine(persistenceDir, "Contexts");
    if (Directory.Exists(contextsDir))
    {
        var contextFiles = Directory.GetFiles(contextsDir, "*.cs", SearchOption.TopDirectoryOnly);
        foreach (string file in contextFiles)
        {
            string content = File.ReadAllText(file);
            // Look for classes that inherit from DbContext
            if (content.Contains(": DbContext") || content.Contains(":DbContext"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.EndsWith("Context") || fileName.EndsWith("DbContext"))
                {
                    dbContexts.Add(fileName);
                }
            }
        }
    }

    // Also look in the root of persistence project
    var rootFiles = Directory.GetFiles(persistenceDir, "*.cs", SearchOption.TopDirectoryOnly);
    foreach (string file in rootFiles)
    {
        string content = File.ReadAllText(file);
        if (content.Contains(": DbContext") || content.Contains(":DbContext"))
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.EndsWith("Context") || fileName.EndsWith("DbContext"))
            {
                if (!dbContexts.Contains(fileName))
                {
                    dbContexts.Add(fileName);
                }
            }
        }
    }

    return dbContexts;
}

static void HandleMultipleDbContexts(string currentDir, string persistenceProject, string webApiProject, string migrationName)
{
    var dbContexts = GetDbContexts(persistenceProject);

    if (dbContexts.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ No DbContext classes found in the project.");
        Console.ResetColor();
        return;
    }

    if (dbContexts.Count == 1)
    {
        // Only one DbContext found, use it directly
        RunMigrationWithContext(currentDir, persistenceProject, webApiProject, migrationName, dbContexts[0]);
        return;
    }

    // Multiple DbContexts found, ask user to choose
    Console.WriteLine();
    Console.WriteLine("🔍 Multiple DbContext classes found:");
    for (int i = 0; i < dbContexts.Count; i++)
    {
        Console.WriteLine($"{i + 1}) {dbContexts[i]}");
    }

    Console.WriteLine();
    Console.Write("Which DbContext do you want to use for migration? (1-" + dbContexts.Count + "): ");

    string? choice = Console.ReadLine()?.Trim();
    if (int.TryParse(choice, out int selectedIndex) && selectedIndex >= 1 && selectedIndex <= dbContexts.Count)
    {
        string selectedContext = dbContexts[selectedIndex - 1];
        Console.WriteLine($"🎯 Using DbContext: {selectedContext}");
        RunMigrationWithContext(currentDir, persistenceProject, webApiProject, migrationName, selectedContext);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ Invalid selection.");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("1) Try again");
        Console.WriteLine("0) Return to main menu");
        Console.Write("Choose an option: ");

        var retryChoice = Console.ReadLine();
        if (retryChoice == "1")
        {
            HandleMultipleDbContexts(currentDir, persistenceProject, webApiProject, migrationName);
        }
    }
}

static void RunMigrationWithContext(string currentDir, string persistenceProject, string webApiProject, string migrationName, string dbContextName)
{
    Console.WriteLine();
    Console.WriteLine("🚀 Creating migration with specific DbContext...");

    try
    {
        var addResult = RunDotnetCommand(currentDir, $"dotnet ef migrations add {migrationName} --context {dbContextName} --project {persistenceProject} --startup-project {webApiProject}");

        if (addResult.Success && addResult.Error.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Migration '{migrationName}' created successfully with {dbContextName}!");
            Console.ResetColor();
            Console.WriteLine();

            // Ask if user wants to update database
            Console.Write("Update database now? [Y/n]: ");
            string? updateChoice = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(updateChoice) || updateChoice.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("🔄 Updating database...");

                try
                {
                    var updateResult = RunDotnetCommand(currentDir, $"dotnet ef database update --context {dbContextName} --project {persistenceProject} --startup-project {webApiProject}");

                    if (updateResult.Success && updateResult.Error.Length == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✅ Database updated successfully!");
                        Console.ResetColor();

                        Console.WriteLine();
                        Console.WriteLine("Press any key to return to main menu...");
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ Database update failed!");
                        Console.ResetColor();

                        string errorMessage = updateResult.Error.ToLowerInvariant();

                        if (errorMessage.Contains("connection") || errorMessage.Contains("server") ||
                            errorMessage.Contains("timeout") || errorMessage.Contains("network"))
                        {
                            Console.WriteLine();
                            Console.WriteLine("💡 Please check your database connection string in appsettings.json");
                            Console.WriteLine("   Make sure your database server is running and accessible.");
                        }
                        else if (errorMessage.Contains("login") || errorMessage.Contains("authentication") ||
                                errorMessage.Contains("password") || errorMessage.Contains("user"))
                        {
                            Console.WriteLine();
                            Console.WriteLine("💡 Please check your database credentials in appsettings.json");
                            Console.WriteLine("   Verify username and password are correct.");
                        }
                        else if (errorMessage.Contains("database") && errorMessage.Contains("exist"))
                        {
                            Console.WriteLine();
                            Console.WriteLine("💡 The target database does not exist.");
                            Console.WriteLine("   Please create the database or check the database name in connection string.");
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.WriteLine("💡 Please check your database connection and try again.");
                            if (!string.IsNullOrEmpty(updateResult.Error))
                            {
                                Console.WriteLine($"   Error details: {updateResult.Error.Trim()}");
                            }
                        }

                        Console.WriteLine();
                        Console.WriteLine("1) Try again");
                        Console.WriteLine("0) Return to main menu");
                        Console.Write("Choose an option: ");

                        var retryChoice = Console.ReadLine();
                        if (retryChoice == "1")
                        {
                            RunMigrationInteractive();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Database update failed!");
                    Console.WriteLine($"💡 Please check your database connection: {ex.Message}");
                    Console.ResetColor();

                    Console.WriteLine();
                    Console.WriteLine("1) Try again");
                    Console.WriteLine("0) Return to main menu");
                    Console.Write("Choose an option: ");

                    var retryChoice = Console.ReadLine();
                    if (retryChoice == "1")
                    {
                        RunMigrationInteractive();
                        return;
                    }
                }
            }
            else
            {
                Console.WriteLine("ℹ️  Database update skipped. Run 'dotnet ef database update' manually when ready.");
                Console.WriteLine();
                Console.WriteLine("Press any key to return to main menu...");
                Console.ReadKey();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Migration creation failed!");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine("💡 Please check your project configuration and try again.");
            if (!string.IsNullOrEmpty(addResult.Error))
            {
                Console.WriteLine($"   Error details: {addResult.Error.Trim()}");
            }

            Console.WriteLine();
            Console.WriteLine("1) Try again");
            Console.WriteLine("0) Return to main menu");
            Console.Write("Choose an option: ");

            var retryChoice = Console.ReadLine();
            if (retryChoice == "1")
            {
                RunMigrationInteractive();
                return;
            }
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Error: {ex.Message}");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("1) Try again");
        Console.WriteLine("0) Return to main menu");
        Console.Write("Choose an option: ");

        var retryChoice = Console.ReadLine();
        if (retryChoice == "1")
        {
            RunMigrationInteractive();
            return;
        }
    }
}

static void RunProjectInteractive()
{
    Console.WriteLine();
    Console.WriteLine("Run Project (WebAPI)");
    Console.WriteLine("--------------------");

    // Check if we're in a valid .NET project directory
    string currentDir = Directory.GetCurrentDirectory();

    // Look for WebAPI projects (exclude core projects, only include project/ directory)
    var webApiFiles = Directory.GetFiles(currentDir, "*.csproj", SearchOption.AllDirectories)
        .Where(f => Path.GetFileName(f).Contains(".WebAPI") || Path.GetFileName(f).Contains("WebAPI"))
        .Where(f =>
        {
            string relativePath = Path.GetRelativePath(currentDir, f);
            return relativePath.StartsWith("project/", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith("project\\", StringComparison.OrdinalIgnoreCase);
        })
        .ToArray();

    if (webApiFiles.Length == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ No WebAPI project found in project/ directory.");
        Console.ResetColor();
        Console.WriteLine("Please run this command from your solution root directory.");
        Console.WriteLine("Looking for projects like: project/ProjectName.WebAPI/ProjectName.WebAPI.csproj");
        Console.WriteLine();
        Console.WriteLine("Press any key to return to main menu...");
        Console.ReadKey();
        return;
    }

    if (webApiFiles.Length > 1)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  Multiple WebAPI projects found:");
        Console.ResetColor();

        for (int i = 0; i < webApiFiles.Length; i++)
        {
            string relativePath = Path.GetRelativePath(currentDir, webApiFiles[i]);
            Console.WriteLine($"  {i + 1}) {relativePath}");
        }

        Console.Write("Select project to run [1]: ");
        string? choice = Console.ReadLine()?.Trim();

        if (!string.IsNullOrEmpty(choice) && int.TryParse(choice, out int selectedIndex) &&
            selectedIndex >= 1 && selectedIndex <= webApiFiles.Length)
        {
            // Use selected project (1-based index)
            webApiFiles = new[] { webApiFiles[selectedIndex - 1] };
        }
        else
        {
            // Default to first project
            webApiFiles = new[] { webApiFiles[0] };
        }
    }

    string webApiProject = webApiFiles[0];
    string projectPath = Path.GetRelativePath(currentDir, webApiProject);

    Console.WriteLine($"🚀 Running project: {projectPath}");
    Console.WriteLine();
    Console.WriteLine("Press Ctrl+C to stop the application.");
    Console.WriteLine();

    try
    {
        var processInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{webApiProject}\"",
            WorkingDirectory = currentDir,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        using var process = System.Diagnostics.Process.Start(processInfo);
        if (process == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Failed to start the project.");
            Console.ResetColor();
            return;
        }

        // Wait for the process to exit (Ctrl+C will terminate it)
        process.WaitForExit();

        Console.WriteLine();
        Console.WriteLine("🛑 Application stopped.");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Error running project: {ex.Message}");
        Console.ResetColor();
    }

    Console.WriteLine();
    Console.WriteLine("Press any key to return to main menu...");
    Console.ReadKey();
}

static void PrintHelp()
{
    Console.WriteLine("Archigen CLI");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  archigen menu  # Interactive guided mode");
    Console.WriteLine("  archigen new <ProjectName> [--output path] [--force]  # Generate a new project from template");
    Console.WriteLine("  archigen layout-test <solutionRoot> <projectName>");
    Console.WriteLine("  archigen parse-entity <entityFile>");
    Console.WriteLine("  archigen crud <ProjectName> <EntityName> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --solution   Solution root (defaults to current directory)");
    Console.WriteLine("  --props      Property definitions (Name:type,Count:int?)");
    Console.WriteLine("  --dbcontext  DbContext name (default BaseDbContext)");
    Console.WriteLine("  --id         Id type (default int)");
}

static void RunAddPropertyInteractive()
{
    Console.WriteLine();
    Console.WriteLine("Add Property to Existing Domain");
    Console.WriteLine("===============================");

    try { Console.Clear(); } catch { }

    Console.WriteLine("Add Property to Existing Domain");
    Console.WriteLine("===============================");
    Console.WriteLine();

    // Get solution root
    string defaultRoot = Directory.GetCurrentDirectory();
    Console.Write($"Solution root [default: {defaultRoot}]: ");
    string? solutionRoot = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(solutionRoot))
        solutionRoot = defaultRoot;

    if (!Directory.Exists(solutionRoot))
    {
        Console.WriteLine($"❌ Directory not found: {solutionRoot}");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    // Detect projects by scanning '<root>/project' for '*.<Layer>' folders and deducing base names
    string projectParent = Path.Combine(solutionRoot, "project");
    if (!Directory.Exists(projectParent))
    {
        Console.WriteLine($"❌ Could not find 'project' folder under: {solutionRoot}");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    string[] domainDirs = Directory.GetDirectories(projectParent, "*.Domain", SearchOption.TopDirectoryOnly);
    if (domainDirs.Length == 0)
    {
        Console.WriteLine("❌ No Domain projects found under 'project'.");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    var projectNames = domainDirs
        .Select(Path.GetFileName)
        .Where(n => n != null)
        .Select(n => n!.Replace(".Domain", string.Empty))
        .OrderBy(n => n)
        .ToList();

    Console.WriteLine("Available projects:");
    for (int i = 0; i < projectNames.Count; i++)
        Console.WriteLine($"  {i + 1}) {projectNames[i]}");
    Console.Write("Select project by number: ");

    string? projectInput = Console.ReadLine()?.Trim();
    if (!int.TryParse(projectInput, out int projectIndex) || projectIndex < 1 || projectIndex > projectNames.Count)
    {
        Console.WriteLine("❌ Invalid project selection!");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    string projectName = projectNames[projectIndex - 1];
    Console.WriteLine($"✅ Selected project: {projectName}");
    Console.WriteLine();

    // Create project layout
    ProjectLayout layout;
    try
    {
        layout = ProjectLayout.Create(solutionRoot, projectName);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to create project layout: {ex.Message}");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    // Find existing entities
    string entitiesDir = Path.Combine(layout.DomainPath, "Entities");
    if (!Directory.Exists(entitiesDir))
    {
        Console.WriteLine("❌ No entities directory found!");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    var entityFiles = Directory.GetFiles(entitiesDir, "*.cs")
        .Where(f => !Path.GetFileName(f).Equals("Entity.cs", StringComparison.OrdinalIgnoreCase))
        .Select(f => Path.GetFileNameWithoutExtension(f))
        .ToList();

    if (entityFiles.Count == 0)
    {
        Console.WriteLine("❌ No domain entities found!");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    // Show available entities
    Console.WriteLine("Available domain entities:");
    for (int i = 0; i < entityFiles.Count; i++)
    {
        Console.WriteLine($"  {i + 1}) {entityFiles[i]}");
    }
    Console.WriteLine();

    // Get entity selection
    Console.Write("Select entity (number): ");
    string? entityInput = Console.ReadLine()?.Trim();

    if (!int.TryParse(entityInput, out int entityIndex) || entityIndex < 1 || entityIndex > entityFiles.Count)
    {
        Console.WriteLine("❌ Invalid entity selection!");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    string selectedEntity = entityFiles[entityIndex - 1];
    Console.WriteLine($"✅ Selected entity: {selectedEntity}");
    Console.WriteLine();

    // Get property definition
    Console.WriteLine("Property definition format: PropertyName:Type");
    Console.WriteLine("Examples:");
    Console.WriteLine("  - Model:string?");
    Console.WriteLine("  - Price:decimal");
    Console.WriteLine("  - IsActive:bool");
    Console.WriteLine("  - CreatedAt:DateTime");
    Console.WriteLine();
    Console.Write("Enter property definition: ");

    string? propertyInput = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(propertyInput))
    {
        Console.WriteLine("❌ Property definition cannot be empty!");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    // Parse property
    var propertyParts = propertyInput.Split(':', 2);
    if (propertyParts.Length != 2)
    {
        Console.WriteLine("❌ Invalid property format! Use PropertyName:Type");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    string propertyName = propertyParts[0].Trim();
    string propertyType = propertyParts[1].Trim();

    if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(propertyType))
    {
        Console.WriteLine("❌ Property name and type cannot be empty!");
        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"🔄 Adding property '{propertyName}: {propertyType}' to {selectedEntity}...");
    Console.WriteLine();

    try
    {
        // Validate inputs before making changes
        ValidatePropertyName(propertyName, selectedEntity);
        ValidatePropertyType(propertyType);

        Console.WriteLine("🔍 Pre-flight checks passed. Starting property addition...");
        Console.WriteLine();

        // Add property to the entity
        AddPropertyToEntity(layout, selectedEntity, propertyName, propertyType);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✅ Property '{propertyName}: {propertyType}' added successfully to {selectedEntity}!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("🎉 Updated components:");
        Console.WriteLine($"   • Domain Entity: {selectedEntity}.cs");
        Console.WriteLine($"   • All Commands and Responses");
        Console.WriteLine($"   • All DTOs and constructors");
        Console.WriteLine($"   • Entity Configuration (EF Core mapping)");
        Console.WriteLine($"   • Validation rules in Command validators");
        Console.WriteLine($"   • Business rules checks");
        Console.WriteLine();
        Console.WriteLine("🚀 Next steps:");
        Console.WriteLine("   1. Review generated validation rules and adjust if needed");
        Console.WriteLine("   2. Create and run a new database migration:");
        Console.WriteLine($"      dotnet ef migrations add Add{propertyName}To{selectedEntity}");
        Console.WriteLine("   3. Update database:");
        Console.WriteLine("      dotnet ef database update");
        Console.WriteLine("   4. Build and test your application");
        Console.WriteLine("   5. Test API endpoints to ensure new property works correctly");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️  Important Notes:");
        Console.WriteLine("   • If you encounter issues, you can manually revert changes before running migration");
        Console.WriteLine("   • Review validation rules in *CommandValidator.cs files");
        Console.WriteLine("   • Check mapping profiles if using complex data transformations");
        Console.ResetColor();
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Failed to add property: {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("💡 Troubleshooting tips:");
        Console.WriteLine("   • Ensure the property name follows PascalCase convention");
        Console.WriteLine("   • Check that the property type is valid (e.g., 'string', 'int', 'DateTime?')");
        Console.WriteLine("   • Verify the entity exists and has proper structure");
        Console.WriteLine("   • Make sure you have write permissions to the project files");
        Console.ResetColor();
        Console.WriteLine();
    }

    Console.WriteLine("Press any key to return to menu...");
    Console.ReadKey();
}

static void AddPropertyToEntity(ProjectLayout layout, string entityName, string propertyName, string propertyType)
{
    // 1. Update Domain Entity
    string entityPath = Path.Combine(layout.DomainPath, "Entities", $"{entityName}.cs");
    if (!File.Exists(entityPath))
    {
        throw new FileNotFoundException($"Entity file not found: {entityPath}");
    }

    string entityContent = File.ReadAllText(entityPath);

    // Check if property already exists
    if (entityContent.Contains($"public {propertyType} {propertyName}"))
    {
        Console.WriteLine($"⚠️  Property {propertyName} already exists in {entityName}.cs");
        return;
    }

    // Use the smart insertion logic for domain entity too
    string propertyDeclaration = $"    public {propertyType} {propertyName} {{ get; set; }}";

    // Find the best insertion point
    int insertionPoint = FindPropertyInsertionPoint(entityContent);

    if (insertionPoint == -1)
    {
        throw new InvalidOperationException("Could not find suitable insertion point in entity file");
    }

    // Insert the new property at the found point
    string updatedEntityContent = entityContent.Insert(insertionPoint, $"{propertyDeclaration}\n");

    // Also update constructors if needed
    updatedEntityContent = UpdateEntityConstructors(updatedEntityContent, propertyName, propertyType);

    File.WriteAllText(entityPath, updatedEntityContent);

    Console.WriteLine($"✅ Updated domain entity: {entityName}.cs");

    // 2. Parse the updated entity to get full definition
    EntityDefinition entity = EntityParser.Parse(entityPath);

    // 3. Update Application layer artifacts
    UpdateApplicationArtifacts(layout, entity, propertyName, propertyType);

    Console.WriteLine("✅ Updated Application layer artifacts");
}

static void UpdateApplicationArtifacts(ProjectLayout layout, EntityDefinition entity, string propertyName, string propertyType)
{
    string featureRoot = Path.Combine(layout.ApplicationPath, "Features", entity.PluralPascal);

    if (!Directory.Exists(featureRoot))
    {
        Console.WriteLine($"⚠️  Feature directory not found: {featureRoot}");
        return;
    }

    // Update Create Command
    UpdateCreateCommand(featureRoot, entity, propertyName, propertyType);

    // Update Update Command
    UpdateUpdateCommand(featureRoot, entity, propertyName, propertyType);

    // Update DTOs
    UpdateDtos(featureRoot, entity, propertyName, propertyType);

    // Update Mapping Profiles
    UpdateMappingProfiles(featureRoot, entity, propertyName, propertyType);

    // Update Entity Configuration (NEW)
    UpdateEntityConfiguration(layout, entity, propertyName, propertyType);

    // Update DbContext (NEW)
    UpdateDbContextForNewProperty(layout, entity);

    // Update Business Rules (NEW)
    UpdateBusinessRules(featureRoot, entity, propertyName, propertyType);

    // Update Validation Rules (NEW)
    UpdateValidationRules(featureRoot, entity, propertyName, propertyType);

    // Update API Controller documentation (NEW)
    UpdateApiControllerDocumentation(layout, entity, propertyName, propertyType);
}

static void UpdateCreateCommand(string featureRoot, EntityDefinition entity, string propertyName, string propertyType)
{
    string commandPath = Path.Combine(featureRoot, "Commands", "Create", $"Create{entity.NamePascal}Command.cs");
    if (File.Exists(commandPath))
    {
        AddPropertyToClass(commandPath, propertyName, propertyType);
        Console.WriteLine($"✅ Updated Create{entity.NamePascal}Command.cs");
    }
}

static void UpdateUpdateCommand(string featureRoot, EntityDefinition entity, string propertyName, string propertyType)
{
    string commandPath = Path.Combine(featureRoot, "Commands", "Update", $"Update{entity.NamePascal}Command.cs");
    if (File.Exists(commandPath))
    {
        AddPropertyToClass(commandPath, propertyName, propertyType);
        Console.WriteLine($"✅ Updated Update{entity.NamePascal}Command.cs");
    }
}

static void UpdateDtos(string featureRoot, EntityDefinition entity, string propertyName, string propertyType)
{
    // Update Create Response
    string createResponsePath = Path.Combine(featureRoot, "Commands", "Create", $"Created{entity.NamePascal}Response.cs");
    if (File.Exists(createResponsePath))
    {
        AddPropertyToClass(createResponsePath, propertyName, propertyType);
        Console.WriteLine($"✅ Updated Created{entity.NamePascal}Response.cs");
    }

    // Update Update Response
    string updateResponsePath = Path.Combine(featureRoot, "Commands", "Update", $"Updated{entity.NamePascal}Response.cs");
    if (File.Exists(updateResponsePath))
    {
        AddPropertyToClass(updateResponsePath, propertyName, propertyType);
        Console.WriteLine($"✅ Updated Updated{entity.NamePascal}Response.cs");
    }

    // Update Get responses
    string getByIdResponsePath = Path.Combine(featureRoot, "Queries", "GetById", $"GetById{entity.NamePascal}Response.cs");
    if (File.Exists(getByIdResponsePath))
    {
        AddPropertyToClass(getByIdResponsePath, propertyName, propertyType);
        Console.WriteLine($"✅ Updated GetById{entity.NamePascal}Response.cs");
    }

    string getListItemPath = Path.Combine(featureRoot, "Queries", "GetList", $"GetList{entity.NamePascal}ListItemDto.cs");
    if (File.Exists(getListItemPath))
    {
        AddPropertyToClass(getListItemPath, propertyName, propertyType);
        Console.WriteLine($"✅ Updated GetList{entity.NamePascal}ListItemDto.cs");
    }
}

static void UpdateMappingProfiles(string featureRoot, EntityDefinition entity, string propertyName, string propertyType)
{
    string mappingPath = Path.Combine(featureRoot, "Profiles", "MappingProfiles.cs");
    if (File.Exists(mappingPath))
    {
        // AutoMapper genelde otomatik eşleme yapar, ancak özel mapping gerekebilir
        string content = File.ReadAllText(mappingPath);

        // Mapping profiles'da özel bir konfigürasyon gerekip gerekmediğini kontrol et
        bool needsCustomMapping = CheckIfNeedsCustomMapping(propertyType);

        if (needsCustomMapping)
        {
            Console.WriteLine($"⚠️  Consider adding custom mapping for {propertyName} ({propertyType}) in MappingProfiles.cs");
            Console.WriteLine($"    Example: .ForMember(dest => dest.{propertyName}, opt => opt.MapFrom(src => src.{propertyName}))");
        }
        else
        {
            Console.WriteLine($"✅ AutoMapper will handle {propertyName} automatically in MappingProfiles.cs");
        }

        // Check if any mapping configurations need updates (especially for complex types)
        if (content.Contains("ForMember") && needsCustomMapping)
        {
            Console.WriteLine($"ℹ️  Existing custom mappings found - verify {propertyName} mapping consistency");
        }
    }
}

static bool CheckIfNeedsCustomMapping(string propertyType)
{
    string baseType = propertyType.TrimEnd('?').ToLowerInvariant();

    return baseType switch
    {
        "datetime" => true,  // Might need timezone handling
        "dateonly" => true,  // Newer .NET type, might need custom mapping
        "timeonly" => true,  // Newer .NET type, might need custom mapping
        "decimal" => true,   // Precision might need attention
        "guid" => false,     // AutoMapper handles this well
        "string" => false,   // AutoMapper handles this well
        "int" => false,      // AutoMapper handles this well
        "long" => false,     // AutoMapper handles this well
        "bool" => false,     // AutoMapper handles this well
        "double" => false,   // AutoMapper handles this well
        "float" => false,    // AutoMapper handles this well
        _ => true            // Unknown types should be checked manually
    };
}

static void AddPropertyToClass(string filePath, string propertyName, string propertyType)
{
    string content = File.ReadAllText(filePath);

    // Check if property already exists
    if (content.Contains($"public {propertyType} {propertyName}"))
    {
        Console.WriteLine($"⚠️  Property {propertyName} already exists in {Path.GetFileName(filePath)}");
        return;
    }

    string propertyDeclaration = $"    public {propertyType} {propertyName} {{ get; set; }}";

    // Find the best insertion point
    int insertionPoint = FindPropertyInsertionPoint(content);

    if (insertionPoint == -1)
    {
        Console.WriteLine($"⚠️  Could not find suitable insertion point in {Path.GetFileName(filePath)}");
        return;
    }

    // Insert the new property at the found point
    string updatedContent = content.Insert(insertionPoint, $"{propertyDeclaration}\n");

    // Update constructors based on file type
    string fileName = Path.GetFileName(filePath);
    if (fileName.Contains("Command"))
    {
        updatedContent = UpdateConstructorsInClass(updatedContent, propertyName, propertyType);
    }
    else if (fileName.Contains("Response") || fileName.Contains("Dto"))
    {
        updatedContent = UpdateConstructorsInResponseDto(updatedContent, propertyName, propertyType);
    }

    File.WriteAllText(filePath, updatedContent);
}

static int FindPropertyInsertionPoint(string content)
{
    string[] lines = content.Split('\n');
    int lastPropertyLine = -1;
    int firstConstructorLine = -1;
    int firstMethodLine = -1;

    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();

        // Find last property line
        if (line.StartsWith("public ") && line.Contains("{ get; set; }"))
        {
            lastPropertyLine = i;
        }
        // Find first constructor line (but after properties)
        else if (firstConstructorLine == -1 && lastPropertyLine != -1 &&
                 (line.StartsWith("public ") && line.Contains("(") && line.Contains(")")))
        {
            firstConstructorLine = i;
        }
        // Find first method line (but after properties)
        else if (firstMethodLine == -1 && lastPropertyLine != -1 &&
                 (line.StartsWith("public ") || line.StartsWith("private ") || line.StartsWith("protected ")) &&
                 !line.Contains("{ get; set; }") && !line.Contains("=>") &&
                 line.Contains("(") && line.Contains(")"))
        {
            firstMethodLine = i;
        }
    }

    // Determine insertion point
    int targetLine = -1;

    if (lastPropertyLine != -1)
    {
        // Insert after the last property
        targetLine = lastPropertyLine + 1;
    }
    else if (firstConstructorLine != -1)
    {
        // Insert before first constructor if no properties found
        targetLine = firstConstructorLine;
    }
    else if (firstMethodLine != -1)
    {
        // Insert before first method if no properties or constructors found
        targetLine = firstMethodLine;
    }

    if (targetLine == -1)
    {
        return -1;
    }

    // Find the character position for the target line
    int charPosition = 0;
    for (int i = 0; i < targetLine && i < lines.Length; i++)
    {
        charPosition += lines[i].Length + 1; // +1 for the newline character
    }

    // Add proper indentation and newline
    return charPosition;
}

static string UpdateEntityConstructors(string content, string propertyName, string propertyType)
{
    string[] lines = content.Split('\n');
    var updatedLines = new List<string>();
    bool inParameterlessConstructor = false;
    bool inParameterizedConstructor = false;
    bool isString = propertyType.TrimEnd('?').Equals("string", StringComparison.OrdinalIgnoreCase);

    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i];
        string trimmedLine = line.Trim();

        // Look for parameterless constructor
        if (trimmedLine.StartsWith("public ") &&
            trimmedLine.Contains("()") &&
            trimmedLine.Contains(")"))
        {
            inParameterlessConstructor = true;
        }
        // Look for parameterized constructor (one that has parameters)
        else if (trimmedLine.StartsWith("public ") &&
            trimmedLine.Contains("(") &&
            !trimmedLine.Contains("()") && // not default constructor
            trimmedLine.Contains(")"))
        {
            inParameterizedConstructor = true;

            // Add parameter to constructor signature (nullable parameters go to end)
            if (trimmedLine.EndsWith(")"))
            {
                // Constructor ends on same line
                string camelCaseParam = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
                string newParam = $"{propertyType} {camelCaseParam}";
                string constructorWithNewParam = trimmedLine.Replace(")", $", {newParam})");
                line = line.Replace(trimmedLine, constructorWithNewParam);
            }
        }
        // End of constructor - add assignments BEFORE closing brace
        else if ((inParameterlessConstructor || inParameterizedConstructor) && trimmedLine == "}")
        {
            // Add assignment for parameterless constructor (only strings get string.Empty)
            if (inParameterlessConstructor && isString)
            {
                string indent = "        "; // Match existing indentation
                updatedLines.Add($"{indent}{propertyName} = string.Empty;");
            }
            // Add assignment for parameterized constructor
            else if (inParameterizedConstructor)
            {
                string indent = "        "; // Match existing indentation
                string camelCasePropertyName = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
                updatedLines.Add($"{indent}{propertyName} = {camelCasePropertyName};");
            }

            inParameterlessConstructor = false;
            inParameterizedConstructor = false;
        }

        updatedLines.Add(line);
    }

    return string.Join("\n", updatedLines);
}

static string UpdateConstructorsInResponseDto(string content, string propertyName, string propertyType)
{
    var lines = content.Split('\n').ToList();
    bool isString = propertyType.TrimEnd('?').Equals("string", StringComparison.OrdinalIgnoreCase);
    string camelCasePropertyName = propertyName.ToCamelCase();

    for (int i = 0; i < lines.Count; i++)
    {
        string line = lines[i].Trim();

        // Find parameterless constructor (Response/DTO style)
        if (line.StartsWith("public ") && line.Contains("()"))
        {
            // Find the closing brace of this constructor and add initialization for strings
            int braceCount = 0;
            bool foundOpenBrace = false;

            for (int j = i + 1; j < lines.Count; j++)
            {
                if (lines[j].Contains("{"))
                {
                    foundOpenBrace = true;
                    braceCount++;
                }
                if (lines[j].Contains("}"))
                {
                    braceCount--;
                }

                if (foundOpenBrace && braceCount == 0 && lines[j].Trim() == "}")
                {
                    // This is the end of the parameterless constructor
                    if (isString)
                    {
                        lines.Insert(j, $"        {propertyName} = string.Empty;");
                    }
                    break;
                }
            }
        }
        // Find parameterized constructor (Response/DTO style)
        else if (line.StartsWith("public ") && line.Contains("(") && !line.Contains("()") && line.Contains(")"))
        {
            // Add new parameter to constructor signature
            string newParam = $"{propertyType} {camelCasePropertyName}";

            if (!line.Contains(camelCasePropertyName))
            {
                string beforeParen = line.Substring(0, line.IndexOf('('));
                string paramsPart = line.Substring(line.IndexOf('(') + 1, line.IndexOf(')') - line.IndexOf('(') - 1).Trim();
                string afterParen = line.Substring(line.IndexOf(')'));

                string updatedParams = string.IsNullOrEmpty(paramsPart) ? newParam : paramsPart + ", " + newParam;
                lines[i] = beforeParen + "(" + updatedParams + afterParen;
            }

            // Find the closing brace of this constructor and add assignment
            int braceCount = 0;
            bool foundOpenBrace = false;

            for (int j = i + 1; j < lines.Count; j++)
            {
                if (lines[j].Contains("{"))
                {
                    foundOpenBrace = true;
                    braceCount++;
                }
                if (lines[j].Contains("}"))
                {
                    braceCount--;
                }

                if (foundOpenBrace && braceCount == 0 && lines[j].Trim() == "}")
                {
                    // This is the end of the parameterized constructor
                    lines.Insert(j, $"        {propertyName} = {camelCasePropertyName};");
                    break;
                }
            }
        }
    }

    return string.Join("\n", lines);
}

static string UpdateConstructorsInClass(string content, string propertyName, string propertyType)
{
    var lines = content.Split('\n').ToList();
    bool isNullable = propertyType.EndsWith("?");
    bool isString = propertyType.TrimEnd('?').Equals("string", StringComparison.OrdinalIgnoreCase);
    string camelCasePropertyName = propertyName.ToCamelCase();

    bool inHandlerClass = false;

    // Find constructors and update them, but SKIP Handler class constructors
    for (int i = 0; i < lines.Count; i++)
    {
        string line = lines[i].Trim();

        // Detect if we're entering a Handler class
        if (line.Contains("Handler ") && line.Contains("IRequestHandler"))
        {
            inHandlerClass = true;
            continue;
        }

        // Reset when we exit any class (closing brace at class level)
        if (line == "}" && inHandlerClass)
        {
            inHandlerClass = false;
            continue;
        }

        // Skip if we're inside Handler class
        if (inHandlerClass)
        {
            continue;
        }

        // Look for Command constructor signatures ONLY (not Handler constructors)
        if (line.StartsWith("public ") && line.Contains("Command(") && line.Contains(")") && !inHandlerClass)
        {
            string beforeParen = line.Substring(0, line.IndexOf('('));
            string paramsPart = line.Substring(line.IndexOf('(') + 1, line.IndexOf(')') - line.IndexOf('(') - 1).Trim();

            // Handle parameterless constructor
            if (string.IsNullOrEmpty(paramsPart))
            {
                // Find the closing brace of this constructor and add initialization for non-nullable strings
                int braceCount = 0;
                bool foundOpenBrace = false;

                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (lines[j].Contains("{"))
                    {
                        foundOpenBrace = true;
                        braceCount++;
                    }
                    if (lines[j].Contains("}"))
                    {
                        braceCount--;
                    }

                    if (foundOpenBrace && braceCount == 0 && lines[j].Trim() == "}")
                    {
                        // This is the end of the parameterless constructor
                        if (isString)
                        {
                            // Use string.Empty for all string properties (nullable or not) for safety
                            lines.Insert(j, $"        {propertyName} = string.Empty;");
                        }
                        else if (isNullable)
                        {
                            // For non-string nullable properties, use null
                            lines.Insert(j, $"        {propertyName} = null;");
                        }
                        break;
                    }
                }
            }
            // Handle parameterized constructor
            else if (!string.IsNullOrEmpty(paramsPart))
            {
                // Add new parameter to constructor signature (NO default assignments to avoid syntax errors)
                string newParam = $"{propertyType} {camelCasePropertyName}";

                if (!paramsPart.Contains(camelCasePropertyName))
                {
                    string updatedParams = paramsPart + ", " + newParam;
                    string before = line.Substring(0, line.IndexOf('(') + 1);
                    string after = line.Substring(line.IndexOf(')'));
                    lines[i] = before + updatedParams + after;
                }

                // Find the closing brace of this constructor and add assignment
                int braceCount = 0;
                bool foundOpenBrace = false;

                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (lines[j].Contains("{"))
                    {
                        foundOpenBrace = true;
                        braceCount++;
                    }
                    if (lines[j].Contains("}"))
                    {
                        braceCount--;
                    }

                    if (foundOpenBrace && braceCount == 0 && lines[j].Trim() == "}")
                    {
                        // This is the end of the parameterized constructor
                        lines.Insert(j, $"        {propertyName} = {camelCasePropertyName};");
                        break;
                    }
                }
            }
        }
    }

    return string.Join("\n", lines);
}

static void ValidatePropertyName(string propertyName, string? entityName = null)
{
    // 1. Property name cannot be null or empty
    if (string.IsNullOrWhiteSpace(propertyName))
    {
        throw new ArgumentException("Property name cannot be empty.");
    }

    // 2. Property name must follow PascalCase convention (start with uppercase letter)
    if (propertyName.Length > 0 && !char.IsUpper(propertyName[0]))
    {
        throw new ArgumentException($"Property name '{propertyName}' must follow PascalCase convention and start with an uppercase letter. Did you mean '{char.ToUpper(propertyName[0])}{propertyName.Substring(1)}'?");
    }

    // 3. Property name cannot be the same as entity name
    if (!string.IsNullOrEmpty(entityName) &&
        string.Equals(propertyName, entityName, StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException($"Property name '{propertyName}' cannot be the same as entity name '{entityName}'. Please choose a different property name.");
    }

    // 4. Property name should be valid C# identifier
    if (!IsValidCSharpIdentifier(propertyName))
    {
        throw new ArgumentException($"Property name '{propertyName}' is not a valid C# identifier. Use PascalCase and start with a letter.");
    }

    // 5. Check for reserved keywords
    if (IsReservedKeyword(propertyName))
    {
        throw new ArgumentException($"Property name '{propertyName}' is a reserved C# keyword. Please choose a different name.");
    }

    // 6. Check for common inappropriate names
    if (IsInappropriatePropertyName(propertyName))
    {
        throw new ArgumentException($"Property name '{propertyName}' is inappropriate or conflicts with common .NET types/keywords. Please choose a more descriptive name.");
    }
}

static void ValidatePropertyType(string propertyType)
{
    // 1. Property type cannot be null or empty
    if (string.IsNullOrWhiteSpace(propertyType))
    {
        throw new ArgumentException("Property type cannot be empty.");
    }

    // 2. Normalize and validate property type
    string normalizedType = NormalizePropertyType(propertyType);

    if (!IsValidPropertyType(normalizedType))
    {
        var validTypes = new[] { "string", "int", "long", "decimal", "double", "float", "bool", "DateTime", "DateOnly", "TimeOnly", "Guid" };
        var suggestedTypes = string.Join(", ", validTypes);
        throw new ArgumentException($"Property type '{propertyType}' is not valid. Use proper casing like: {suggestedTypes}. Add '?' for nullable types (e.g., 'string?', 'int?').");
    }
}

static void ValidateEntityName(string entityName, List<string> existingEntities)
{
    // 1. Entity name cannot be null or empty
    if (string.IsNullOrWhiteSpace(entityName))
    {
        throw new ArgumentException("Entity name cannot be empty.");
    }

    // 2. Entity name should be valid C# identifier
    if (!IsValidCSharpIdentifier(entityName))
    {
        throw new ArgumentException($"Entity name '{entityName}' is not a valid C# identifier. Use PascalCase and start with a letter.");
    }

    // 3. Check for reserved keywords
    if (IsReservedKeyword(entityName))
    {
        throw new ArgumentException($"Entity name '{entityName}' is a reserved C# keyword. Please choose a different name.");
    }

    // 4. Check if entity already exists
    if (existingEntities.Any(e => string.Equals(e, entityName, StringComparison.OrdinalIgnoreCase)))
    {
        throw new ArgumentException($"Entity '{entityName}' already exists. Please choose a different name.");
    }

    // 5. Check for inappropriate names
    if (IsInappropriateEntityName(entityName))
    {
        throw new ArgumentException($"Entity name '{entityName}' is inappropriate or conflicts with common .NET types. Please choose a more descriptive name.");
    }
}

static bool IsValidCSharpIdentifier(string name)
{
    if (string.IsNullOrEmpty(name))
        return false;

    // Must start with letter or underscore
    if (!char.IsLetter(name[0]) && name[0] != '_')
        return false;

    // Rest must be letters, digits, or underscores
    for (int i = 1; i < name.Length; i++)
    {
        if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
            return false;
    }

    return true;
}

static bool IsReservedKeyword(string name)
{
    var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while"
    };

    return keywords.Contains(name);
}

static bool IsInappropriatePropertyName(string name)
{
    var inappropriateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // .NET types with wrong casing
        "string", "String", "STRING", "int", "Int", "INT", "bool", "Bool", "BOOL",
        "double", "Double", "DOUBLE", "float", "Float", "FLOAT", "decimal", "Decimal", "DECIMAL",
        "long", "Long", "LONG", "short", "Short", "SHORT", "byte", "Byte", "BYTE",
        "char", "Char", "CHAR", "object", "Object", "OBJECT",

        // Common inappropriate names
        "Id", "ID", "id", "Entity", "entity", "Model", "model", "Class", "class",
        "Type", "type", "Value", "value", "Data", "data",

        // Generic inappropriate names
        "Property", "property", "Field", "field", "Variable", "variable"
    };

    return inappropriateNames.Contains(name);
}

static bool IsInappropriateEntityName(string name)
{
    var inappropriateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // .NET types
        "String", "Int", "Bool", "Double", "Float", "Decimal", "Long", "Short", "Byte",
        "Char", "Object", "DateTime", "Guid", "List", "Array", "Dictionary", "Collection",

        // Generic inappropriate names
        "Entity", "Model", "Class", "Type", "Data", "Item", "Record", "Row", "Table",
        "Database", "Repository", "Service", "Controller", "Manager", "Handler"
    };

    return inappropriateNames.Contains(name);
}

static string NormalizePropertyType(string type)
{
    // Handle nullable types
    bool isNullable = type.EndsWith("?");
    string baseType = isNullable ? type.TrimEnd('?') : type;

    // Normalize common type aliases to proper casing using case-insensitive matching
    string normalizedBaseType = baseType.ToLowerInvariant() switch
    {
        "string" => "string",
        "system.string" => "string", // Handle System.String
        "int" or "int32" or "system.int32" => "int",
        "long" or "int64" => "long",
        "bool" or "boolean" => "bool",
        "double" => "double",
        "float" => "float",
        "decimal" => "decimal",
        "datetime" => "DateTime",
        "dateonly" => "DateOnly",
        "timeonly" => "TimeOnly",
        "guid" => "Guid",
        "byte" => "byte",
        "short" or "int16" => "short",
        "char" => "char",
        "object" => "object",
        // Handle common generic collection types
        var t when t.StartsWith("list<") => t.Replace("list<", "List<"),
        var t when t.StartsWith("ilist<") => t.Replace("ilist<", "IList<"),
        var t when t.StartsWith("icollection<") => t.Replace("icollection<", "ICollection<"),
        var t when t.StartsWith("ienumerable<") => t.Replace("ienumerable<", "IEnumerable<"),
        var t when t.StartsWith("dictionary<") => t.Replace("dictionary<", "Dictionary<"),
        var t when t.StartsWith("idictionary<") => t.Replace("idictionary<", "IDictionary<"),
        _ => baseType // Return original if not found in mapping (handles custom types)
    };

    return isNullable ? normalizedBaseType + "?" : normalizedBaseType;
}

static bool IsValidPropertyType(string type)
{
    // Handle nullable types
    bool isNullable = type.EndsWith("?");
    string baseType = isNullable ? type.TrimEnd('?').Trim() : type.Trim();

    // Basic primitive types
    var primitiveTypes = new HashSet<string>
    {
        "string", "int", "long", "decimal", "double", "float", "bool",
        "DateTime", "DateOnly", "TimeOnly", "Guid", "byte", "short", "char", "object"
    };

    if (primitiveTypes.Contains(baseType))
        return true;

    // Array types (e.g., string[], int[])
    if (baseType.EndsWith("[]"))
    {
        string elementType = baseType.Substring(0, baseType.Length - 2);
        return IsValidPropertyType(elementType);
    }

    // Generic collection types (e.g., List<string>, Dictionary<int, string>)
    if (baseType.Contains('<') && baseType.EndsWith('>'))
    {
        var genericTypes = new HashSet<string>
        {
            "List", "IList", "ICollection", "IEnumerable", "HashSet", "ISet",
            "Dictionary", "IDictionary", "KeyValuePair"
        };

        string genericTypeName = baseType.Substring(0, baseType.IndexOf('<'));
        if (genericTypes.Contains(genericTypeName))
        {
            // For now, accept generic collections - proper validation would require parsing type parameters
            return true;
        }
    }

    // Custom types (entities, enums, etc.) - accept PascalCase identifiers
    if (IsValidCSharpIdentifier(baseType) && char.IsUpper(baseType[0]))
    {
        return true;
    }

    return false;
}

static void UpdateEntityConfiguration(ProjectLayout layout, EntityDefinition entity, string propertyName, string propertyType)
{
    string configPath = Path.Combine(layout.PersistencePath, "EntityConfigurations", $"{entity.NamePascal}Configuration.cs");

    if (!File.Exists(configPath))
    {
        Console.WriteLine($"⚠️  Entity configuration not found: {configPath}");
        return;
    }

    string content = File.ReadAllText(configPath);

    // Check if property configuration already exists
    if (content.Contains($"builder.Property(u => u.{propertyName})"))
    {
        Console.WriteLine($"⚠️  Property {propertyName} configuration already exists in {entity.NamePascal}Configuration.cs");
        return;
    }

    // Find the insertion point (after other Property configurations, before HasQueryFilter)
    string[] lines = content.Split('\n');
    int insertIndex = -1;

    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.StartsWith("builder.HasQueryFilter") ||
            line.StartsWith("builder.HasMany") ||
            line.StartsWith("builder.HasBaseType"))
        {
            insertIndex = i;
            break;
        }
    }

    if (insertIndex == -1)
    {
        // Find the last Property configuration line
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Trim().StartsWith("builder.Property("))
            {
                insertIndex = i + 1;
                break;
            }
        }
    }

    if (insertIndex == -1)
    {
        Console.WriteLine($"⚠️  Could not find suitable insertion point in {entity.NamePascal}Configuration.cs");
        return;
    }

    // Create property configuration
    bool isRequired = !propertyType.EndsWith("?");
    string configLine = $"        builder.Property(u => u.{propertyName}).HasColumnName(\"{propertyName}\"){(isRequired ? ".IsRequired()" : "")};";

    // Insert the configuration
    var updatedLines = lines.ToList();
    updatedLines.Insert(insertIndex, configLine);

    string updatedContent = string.Join("\n", updatedLines);
    File.WriteAllText(configPath, updatedContent);

    Console.WriteLine($"✅ Updated {entity.NamePascal}Configuration.cs");
}

static void UpdateDbContextForNewProperty(ProjectLayout layout, EntityDefinition entity)
{
    // Find DbContext file
    string contextsDir = Path.Combine(layout.PersistencePath, "Contexts");
    if (!Directory.Exists(contextsDir))
    {
        Console.WriteLine($"⚠️  Contexts directory not found: {contextsDir}");
        return;
    }

    var contextFiles = Directory.GetFiles(contextsDir, "*DbContext.cs");
    if (contextFiles.Length == 0)
    {
        Console.WriteLine($"⚠️  No DbContext files found in: {contextsDir}");
        return;
    }

    foreach (string contextPath in contextFiles)
    {
        string content = File.ReadAllText(contextPath);

        // Check if entity is already configured in OnModelCreating
        if (content.Contains($"modelBuilder.ApplyConfiguration(new {entity.NamePascal}Configuration())"))
        {
            Console.WriteLine($"ℹ️  {entity.NamePascal} already configured in {Path.GetFileName(contextPath)}");
            continue;
        }

        // Find OnModelCreating method and add configuration if not exists
        if (content.Contains("protected override void OnModelCreating"))
        {
            // This is handled by the initial CRUD generation, not needed for property updates
            Console.WriteLine($"ℹ️  DbContext configuration is handled by Entity Framework conventions for {entity.NamePascal}");
        }
    }
}

static void UpdateBusinessRules(string featureRoot, EntityDefinition entity, string propertyName, string propertyType)
{
    string rulesPath = Path.Combine(featureRoot, "Rules", $"{entity.NamePascal}BusinessRules.cs");

    if (!File.Exists(rulesPath))
    {
        Console.WriteLine($"⚠️  Business rules not found: {rulesPath}");
        return;
    }

    // For now, just inform the user that business rules might need manual updates
    Console.WriteLine($"ℹ️  Check business rules for {propertyName} validation if needed: {entity.NamePascal}BusinessRules.cs");
}

static void UpdateValidationRules(string featureRoot, EntityDefinition entity, string propertyName, string propertyType)
{
    // Look for validator files in Commands
    string[] validatorSearchPaths = {
        Path.Combine(featureRoot, "Commands", "Create", $"Create{entity.NamePascal}CommandValidator.cs"),
        Path.Combine(featureRoot, "Commands", "Update", $"Update{entity.NamePascal}CommandValidator.cs")
    };

    foreach (string validatorPath in validatorSearchPaths)
    {
        if (File.Exists(validatorPath))
        {
            AddValidationRuleToValidator(validatorPath, propertyName, propertyType);
        }
    }
}

static void AddValidationRuleToValidator(string validatorPath, string propertyName, string propertyType)
{
    string content = File.ReadAllText(validatorPath);

    // Check if validation rule already exists
    if (content.Contains($"RuleFor(command => command.{propertyName})"))
    {
        Console.WriteLine($"⚠️  Validation rule for {propertyName} already exists in {Path.GetFileName(validatorPath)}");
        return;
    }

    // Find the constructor body to add validation rule
    string[] lines = content.Split('\n');
    int insertIndex = -1;

    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.StartsWith("RuleFor(") && i > 0)
        {
            // Insert after the last RuleFor
            insertIndex = i + 1;
        }
    }

    if (insertIndex == -1)
    {
        // Look for constructor body closing brace
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "}" && lines[i - 1].Trim().StartsWith("RuleFor("))
            {
                insertIndex = i;
                break;
            }
        }
    }

    if (insertIndex == -1)
    {
        Console.WriteLine($"⚠️  Could not find suitable insertion point in {Path.GetFileName(validatorPath)}");
        return;
    }

    // Generate appropriate validation rule based on property type
    string validationRule = GenerateValidationRule(propertyName, propertyType);

    var updatedLines = lines.ToList();
    updatedLines.Insert(insertIndex, validationRule);

    string updatedContent = string.Join("\n", updatedLines);
    File.WriteAllText(validatorPath, updatedContent);

    Console.WriteLine($"✅ Added validation rule for {propertyName} in {Path.GetFileName(validatorPath)}");
}

static string GenerateValidationRule(string propertyName, string propertyType)
{
    string baseType = propertyType.TrimEnd('?').Trim();
    bool isNullable = propertyType.EndsWith("?");

    // Handle array types
    if (baseType.EndsWith("[]"))
    {
        return $"        RuleFor(command => command.{propertyName}).NotNull();";
    }

    // Handle generic collection types
    if (baseType.Contains('<') && baseType.EndsWith('>'))
    {
        return $"        RuleFor(command => command.{propertyName}).NotNull();";
    }

    return baseType switch
    {
        "string" when !isNullable => $"        RuleFor(command => command.{propertyName}).NotEmpty().MinimumLength(1).MaximumLength(100);",
        "string" when isNullable => $"        RuleFor(command => command.{propertyName}).MaximumLength(100);",
        "int" or "long" when !isNullable => $"        RuleFor(command => command.{propertyName}).GreaterThan(0);",
        "decimal" or "double" or "float" when !isNullable => $"        RuleFor(command => command.{propertyName}).GreaterThan(0);",
        "bool" => $"        // RuleFor(command => command.{propertyName}) - Boolean validation if needed;",
        "DateTime" when !isNullable => $"        RuleFor(command => command.{propertyName}).NotEmpty();",
        "DateTime" when isNullable => $"        RuleFor(command => command.{propertyName}).NotNull();",
        "DateOnly" when !isNullable => $"        RuleFor(command => command.{propertyName}).NotEmpty();",
        "TimeOnly" when !isNullable => $"        RuleFor(command => command.{propertyName}).NotEmpty();",
        "Guid" when !isNullable => $"        RuleFor(command => command.{propertyName}).NotEmpty();",
        "Guid" when isNullable => $"        RuleFor(command => command.{propertyName}).NotNull();",
        _ when !isNullable => $"        RuleFor(command => command.{propertyName}).NotNull();",
        _ => $"        // RuleFor(command => command.{propertyName}) - Add validation if needed;"
    };
}
static void UpdateApiControllerDocumentation(ProjectLayout layout, EntityDefinition entity, string propertyName, string propertyType)
{
    string controllerPath = Path.Combine(layout.WebApiPath, "Controllers", $"{entity.PluralPascal}Controller.cs");

    if (!File.Exists(controllerPath))
    {
        Console.WriteLine($"⚠️  Controller not found: {controllerPath}");
        return;
    }

    // API Controller genelde otomatik olarak DTO'ları kullanır, manuel güncelleme gerekmez
    // Ancak Swagger dokümantasyonu için özel attribute'lar gerekebilir
    Console.WriteLine($"ℹ️  API Controller will automatically use updated DTOs for {propertyName} in {entity.PluralPascal}Controller.cs");
    Console.WriteLine($"ℹ️  Consider adding [SwaggerSchema] attributes if needed for API documentation");
}

