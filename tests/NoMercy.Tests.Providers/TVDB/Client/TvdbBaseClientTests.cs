using System.Reflection;
using NoMercy.Providers.TVDB.Client;

namespace NoMercy.Tests.Providers.TVDB.Client;

/// <summary>
/// PROV-CRIT-03: Tests verifying that TvdbBaseClient.GetToken no longer uses
/// .Result on SendAsync (which mixes sync blocking with async and can deadlock).
/// The fix replaces:
///   await client.SendAsync(msg).Result.Content.ReadAsStringAsync()
/// with:
///   var resp = await client.SendAsync(msg);
///   await resp.Content.ReadAsStringAsync();
/// </summary>
[Trait("Category", "Unit")]
public class TvdbBaseClientTests
{
    [Fact]
    public void GetToken_StateMachine_DoesNotCallTaskResult()
    {
        // Async methods compile into state machine classes (e.g., <GetToken>d__N).
        // If .Result was used on a Task, the state machine IL would contain a call
        // or callvirt to Task<T>.get_Result. We scan the state machine type's
        // MoveNext method IL to verify no such call exists.

        Type clientType = typeof(TvdbBaseClient);

        // Find the compiler-generated state machine for GetToken
        Type? stateMachineType = clientType
            .GetNestedTypes(BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.Contains("GetToken"));

        Assert.NotNull(stateMachineType);

        MethodInfo? moveNext = stateMachineType.GetMethod(
            "MoveNext",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

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
            if (ilBytes[i] != 0x28 && ilBytes[i] != 0x6F) continue;

            int token = BitConverter.ToInt32(ilBytes, i + 1);
            try
            {
                MethodBase? method = module.ResolveMethod(token);
                if (method is null) continue;

                if (method.Name == "get_Result" &&
                    method.DeclaringType is not null &&
                    method.DeclaringType.FullName is not null &&
                    method.DeclaringType.FullName.Contains("Task"))
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

        Assert.False(foundGetResult,
            "PROV-CRIT-03 regression: TvdbBaseClient.GetToken still calls .Result on a Task. " +
            "Use 'await' instead of '.Result' to avoid deadlocks.");
    }

    [Fact]
    public void GetToken_IsAsync_ReturnsTask()
    {
        // Verify GetToken is declared as an async method (returns Task<T>)
        MethodInfo? getTokenMethod = typeof(TvdbBaseClient).GetMethod(
            "GetToken",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(getTokenMethod);

        // Async methods return Task or Task<T>
        Type returnType = getTokenMethod.ReturnType;
        Assert.True(
            returnType == typeof(Task) ||
            (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)),
            "GetToken should be async and return a Task or Task<T>");
    }

    [Fact]
    public void GetToken_StateMachine_HasMultipleAwaiterGetResult()
    {
        // With the fix, GetToken should have TWO awaiter GetResult calls:
        // 1. await client.SendAsync(httpRequestMessage) — awaits Task<HttpResponseMessage>
        // 2. await httpResponse.Content.ReadAsStringAsync() — awaits Task<string>
        // Before the fix, there was only one await (ReadAsStringAsync), because
        // SendAsync was resolved via .Result (synchronous blocking).

        Type clientType = typeof(TvdbBaseClient);

        Type? stateMachineType = clientType
            .GetNestedTypes(BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.Contains("GetToken"));

        Assert.NotNull(stateMachineType);

        MethodInfo? moveNext = stateMachineType.GetMethod(
            "MoveNext",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

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
            if (ilBytes[i] != 0x28 && ilBytes[i] != 0x6F) continue;

            int token = BitConverter.ToInt32(ilBytes, i + 1);
            try
            {
                MethodBase? method = module.ResolveMethod(token);
                if (method is null) continue;

                if (method.Name == "GetResult" &&
                    method.DeclaringType is not null &&
                    method.DeclaringType.FullName is not null &&
                    method.DeclaringType.FullName.Contains("TaskAwaiter"))
                {
                    awaiterGetResultCount++;
                }
            }
            catch
            {
                // Skip unresolvable tokens
            }
        }

        // The fixed code should have at least 2 await points:
        // await SendAsync + await ReadAsStringAsync
        Assert.True(awaiterGetResultCount >= 2,
            $"Expected at least 2 await points (SendAsync + ReadAsStringAsync) in GetToken, " +
            $"but found {awaiterGetResultCount}. The .Result blocking call may still be present.");
    }
}
