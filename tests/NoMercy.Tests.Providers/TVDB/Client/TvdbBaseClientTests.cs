using System.Reflection;
using NoMercy.Providers.TVDB.Client;

namespace NoMercy.Tests.Providers.TVDB.Client;

/// <summary>
/// PROV-CRIT-03: Tests verifying that TvdbBaseClient.LoginAsync does not use
/// .Result on SendAsync (which mixes sync blocking with async and can deadlock).
/// The original bug was in a method called GetToken that has since been renamed to
/// LoginAsync and refactored — these tests guard the same safety contract on the
/// current implementation.
/// </summary>
[Trait("Category", "Unit")]
public class TvdbBaseClientTests
{
    [Fact]
    public void LoginAsync_StateMachine_DoesNotCallTaskResult()
    {
        // Async methods compile into state machine classes (e.g., <LoginAsync>d__N).
        // If .Result was used on a Task, the state machine IL would contain a call
        // or callvirt to Task<T>.get_Result. We scan the state machine type's
        // MoveNext method IL to verify no such call exists.

        Type clientType = typeof(TvdbBaseClient);

        // Find the compiler-generated state machine for LoginAsync
        Type? stateMachineType = clientType
            .GetNestedTypes(BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.Contains("LoginAsync"));

        Assert.NotNull(stateMachineType);

        MethodInfo? moveNext = stateMachineType.GetMethod(
            "MoveNext",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );

        Assert.NotNull(moveNext);

        MethodBody? body = moveNext.GetMethodBody();
        Assert.NotNull(body);

        byte[] ilBytes = body.GetILAsByteArray()!;
        Assert.NotNull(ilBytes);
        Assert.True(ilBytes.Length > 0);

        // Resolve the metadata token for Task<HttpResponseMessage>.get_Result
        // by scanning all method references in the IL for any get_Result call.
        // We look for call/callvirt instructions referencing a method named "get_Result"
        // on a Task-like type.
        bool foundGetResult = false;
        Module module = stateMachineType.Module;

        for (int i = 0; i < ilBytes.Length - 4; i++)
        {
            // call = 0x28, callvirt = 0x6F — both are 5-byte instructions (opcode + 4-byte token)
            if (ilBytes[i] != 0x28 && ilBytes[i] != 0x6F)
                continue;

            int token = BitConverter.ToInt32(ilBytes, i + 1);
            try
            {
                MethodBase? method = module.ResolveMethod(token);
                if (method is null)
                    continue;

                if (
                    method.Name == "get_Result"
                    && method.DeclaringType is not null
                    && method.DeclaringType.FullName is not null
                    && method.DeclaringType.FullName.Contains("Task")
                )
                {
                    foundGetResult = true;
                    break;
                }
            }
            catch
            {
                // ResolveMethod can throw for certain tokens; skip those
            }
        }

        Assert.False(
            foundGetResult,
            "PROV-CRIT-03 regression: TvdbBaseClient.LoginAsync still calls .Result on a Task. "
                + "Use 'await' instead of '.Result' to avoid deadlocks."
        );
    }

    [Fact]
    public void LoginAsync_IsAsync_ReturnsTask()
    {
        // Verify LoginAsync is declared as an async method (returns Task<T>)
        MethodInfo? loginMethod = typeof(TvdbBaseClient).GetMethod(
            "LoginAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(loginMethod);

        // Async methods return Task or Task<T>
        Type returnType = loginMethod.ReturnType;
        Assert.True(
            returnType == typeof(Task)
                || (
                    returnType.IsGenericType
                    && returnType.GetGenericTypeDefinition() == typeof(Task<>)
                ),
            "LoginAsync should be async and return a Task or Task<T>"
        );
    }

    [Fact]
    public void LoginAsync_StateMachine_HasMultipleAwaiterGetResult()
    {
        // With the correct implementation, LoginAsync should have at least TWO await points:
        // 1. await loginClient.SendAsync(request) — awaits Task<HttpResponseMessage>
        // 2. await response.Content.ReadAsStringAsync() — awaits Task<string>
        // Both must be proper awaits, not .Result blocking calls.

        Type clientType = typeof(TvdbBaseClient);

        Type? stateMachineType = clientType
            .GetNestedTypes(BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.Contains("LoginAsync"));

        Assert.NotNull(stateMachineType);

        MethodInfo? moveNext = stateMachineType.GetMethod(
            "MoveNext",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );

        Assert.NotNull(moveNext);

        MethodBody? body = moveNext.GetMethodBody();
        Assert.NotNull(body);

        byte[] ilBytes = body.GetILAsByteArray()!;
        Module module = stateMachineType.Module;

        // Count calls to TaskAwaiter.GetResult() or TaskAwaiter<T>.GetResult()
        // These represent actual await points in the state machine.
        int awaiterGetResultCount = 0;

        for (int i = 0; i < ilBytes.Length - 4; i++)
        {
            if (ilBytes[i] != 0x28 && ilBytes[i] != 0x6F)
                continue;

            int token = BitConverter.ToInt32(ilBytes, i + 1);
            try
            {
                MethodBase? method = module.ResolveMethod(token);
                if (method is null)
                    continue;

                if (
                    method.Name == "GetResult"
                    && method.DeclaringType is not null
                    && method.DeclaringType.FullName is not null
                    && method.DeclaringType.FullName.Contains("TaskAwaiter")
                )
                {
                    awaiterGetResultCount++;
                }
            }
            catch
            {
                // Skip unresolvable tokens
            }
        }

        // The correct implementation has at least 2 await points:
        // await SendAsync + await ReadAsStringAsync
        Assert.True(
            awaiterGetResultCount >= 2,
            $"Expected at least 2 await points (SendAsync + ReadAsStringAsync) in LoginAsync, "
                + $"but found {awaiterGetResultCount}. The .Result blocking call may still be present."
        );
    }
}
