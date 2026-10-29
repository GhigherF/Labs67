using Grpc.Core;

namespace gRPCServer01.Services;

public class CalculatorService : Calculator.CalculatorBase
{
    public override Task<DoubleReply> SUM(BinaryRequest request, ServerCallContext context)
    {
        return Task.FromResult(new DoubleReply { Result = request.X + request.Y });
    }

    public override Task<DoubleReply> SUB(BinaryRequest request, ServerCallContext context)
    {
        return Task.FromResult(new DoubleReply { Result = request.X - request.Y });
    }

    public override Task<DoubleReply> MUL(BinaryRequest request, ServerCallContext context)
    {
        return Task.FromResult(new DoubleReply { Result = request.X * request.Y });
    }

    public override Task<DoubleReply> DIV(BinaryRequest request, ServerCallContext context)
    {
        if (request.Y == 0)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Division by zero is not allowed.")
            );
        }

        return Task.FromResult(new DoubleReply { Result = request.X / request.Y });
    }

    public override Task<IntReply> FACT(FactorialRequest request, ServerCallContext context)
    {
        int x = request.X;

        if (x < 0)
        {
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    "Factorial is not defined for negative integers."
                )
            );
        }

        if (x > 12)
        {
            throw new RpcException(
                new Status(StatusCode.OutOfRange, "Factorial result exceeds Int32.MaxValue.")
            );
        }

        int result = 1;

        for (int i = 2; i <= x; i++)
        {
            result *= i;
        }

        return Task.FromResult(new IntReply { Result = result });
    }
}
