namespace ApiAggregator.Api.Services.Interfaces
{
    public interface IExternalApiService
    {
        string ServiceName { get; }
        Task<object> FetchDataAsync(CancellationToken cancellationToken = default);
    }
}
