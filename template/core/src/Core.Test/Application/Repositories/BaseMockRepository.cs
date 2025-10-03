using AutoMapper;

using Core.Application.Rules;
using Core.Localization.Resource.Yaml;
using Core.Persistence.Repositories;
using Core.Test.Application.FakeData;
using Core.Test.Application.Helpers;

using Microsoft.Extensions.Logging;

using Moq;

namespace Core.Test.Application.Repositories;

public abstract class BaseMockRepository<TRepository, TEntity, TEntityId, TMappingProfile, TBusinessRules, TFakeData>
    where TEntity : Entity<TEntityId>, new()
    where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    where TMappingProfile : Profile, new()
    where TBusinessRules : BaseBusinessRules
    where TFakeData : BaseFakeData<TEntity, TEntityId>, new()
{
    public IMapper Mapper;
    public Mock<TRepository> MockRepository;
    public TBusinessRules BusinessRules;

    public BaseMockRepository(TFakeData fakeData)
    {
        var loggerFactory = LoggerFactory.Create(builder => { });

        MapperConfiguration mapperConfig =
            new(c =>
            {
                c.AddProfile<TMappingProfile>();
            }, loggerFactory);
        Mapper = mapperConfig.CreateMapper();

        MockRepository = MockRepositoryHelper.GetRepository<TRepository, TEntity, TEntityId>(fakeData.Data);
        BusinessRules =
            (TBusinessRules)
                Activator.CreateInstance(
                    type: typeof(TBusinessRules),
                    MockRepository.Object,
                    new ResourceLocalizationManager(resources: []) { AcceptLocales = new[] { "en" } }
                )! ?? throw new InvalidOperationException($"Cannot create an instance of {typeof(TBusinessRules).FullName}.");
    }
}