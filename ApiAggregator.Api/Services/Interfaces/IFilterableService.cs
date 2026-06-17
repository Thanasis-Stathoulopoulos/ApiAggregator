namespace ApiAggregator.Api.Services.Interfaces
{
    public interface IFilterableService
    {
        object? Filter(object data, string keyword);
    }
}
