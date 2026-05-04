using PaymentGateway.Api.Options;

namespace PaymentGateway.Api.Tests;

public class AcquiringBankOptionsTests
{
    [Fact]
    public void DefaultTimeoutSeconds_IsFiveSeconds()
    {
        var options = new AcquiringBankOptions();

        Assert.Equal(5, options.TimeoutSeconds);
    }
}
