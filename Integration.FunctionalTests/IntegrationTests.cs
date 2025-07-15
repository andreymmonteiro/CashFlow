using System.Net.Http.Json;
using System.Text.Json;
using BalanceService.Presentation.Dtos.Response;
using Microsoft.AspNetCore.Hosting;
using TransactionService.Application.Commands;
using static Integration.FunctionalTests.Factory;

namespace Integration.FunctionalTests;

public class IntegrationTests : IClassFixture<TransactionApiFactory>, IClassFixture<ConsolidationApiFactory>, IClassFixture<BalanceApiFactory>
{
    private readonly HttpClient _transactionClient;
    private readonly HttpClient _consolidationClient;
    private readonly HttpClient _balanceClient;

    public IntegrationTests(TransactionApiFactory transactionFactory, ConsolidationApiFactory consolidationFactory, BalanceApiFactory balanceFactory)
    {
        _transactionClient = transactionFactory.WithWebHostBuilder(b => b.UseEnvironment("FunctionalTest")).CreateClient();
        _consolidationClient = consolidationFactory.WithWebHostBuilder(b => b.UseEnvironment("FunctionalTest")).CreateClient();
        _balanceClient = balanceFactory.WithWebHostBuilder(b => b.UseEnvironment("FunctionalTest")).CreateClient();
    }

    [Fact]
    public async Task EndToEnd_Workflow_Succeeds()
    {
        var command = new CreateTransactionCommand(Guid.NewGuid(), 10);

        var transactionResponse = await _transactionClient.PostAsJsonAsync("/api/transactions", command);
        transactionResponse.EnsureSuccessStatusCode();

        // If becomes intermittent, you can increase the delay
        await Task.Delay(1000); // Wait for the transaction to be processed

        var balanceResponse = await _balanceClient.GetAsync($"api/balance?accountId={command.AccountId}");
        balanceResponse.EnsureSuccessStatusCode();

        var json = await balanceResponse.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var balance = JsonSerializer.Deserialize<BalanceResponse>(json, options);

        Assert.Equal(command.Amount, balance.Amount);
    }
}
