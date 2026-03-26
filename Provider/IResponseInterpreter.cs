using PaymentEngine.Models;

namespace PaymentEngine.Provider;

public interface IResponseInterpreter
{
    PaymentResult InterpretResponse(IProviderCallResult callResult, PaymentRequest request);
}