// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using NoMercy.Providers.AcoustId.Client;

namespace NoMercy.Tests.Providers.AcoustId.Client;

/// <summary>
/// PROV-H06: Tests verifying that the dead code (contradictory while loop)
/// has been removed from AcoustIdBaseClient.Get.
/// The bug: A while loop condition required Results.Length == 0 AND Results.Any(...),
/// which is contradictory — an empty array can never have Any() return true.
/// The loop body never executed (dead code).
/// The fix: Remove the while loop and the unused `iteration` variable.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class AcoustIdBaseClientTests
{
    [Fact]
    public void Get_Method_DoesNotContain_WhileLoop_WithContradictoryCondition()
    {
        // The while loop in the original code used a local variable `iteration`
        // that was incremented. After removing the dead while loop, the method
        // should have fewer local variables (no `iteration` int).
        MethodInfo? getMethod = typeof(AcoustIdBaseClient).GetMethod(
            name: "Get",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: getMethod);

        // Get is async, so we need to find the state machine type
        Type? stateMachineType = getMethod
            .GetCustomAttribute<AsyncStateMachineAttribute>()
            ?.StateMachineType;

        Assert.NotNull(@object: stateMachineType);

        // The state machine's MoveNext method contains the actual compiled IL
        MethodInfo? moveNext = stateMachineType.GetMethod(
            name: "MoveNext",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: moveNext);

        MethodBody? body = moveNext.GetMethodBody();
        Assert.NotNull(@object: body);

        // Get the IL bytes and look for branch-back instructions (while loop pattern).
        // A while loop in IL compiles to: condition check → branch forward (skip body) or
        // branch back (re-enter loop). The contradictory while loop would have had
        // a branch-back (br/br.s) to re-evaluate the condition after iteration++.
        //
        // Instead of fragile IL parsing, we verify the `iteration` local variable
        // is gone. The original code had `int iteration = 0;` and `iteration++` inside
        // the while loop. After removing the while loop, there should be no local
        // variable named "iteration" in the state machine fields.
        FieldInfo? iterationField = stateMachineType.GetField(
            name: "<iteration>5__",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
        );

        // Also check with other possible compiler-generated name patterns
        FieldInfo[] allFields = stateMachineType.GetFields(
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
        );

        bool hasIterationField = allFields.Any(predicate: f =>
            f.Name.Contains(value: "iteration", comparisonType: StringComparison.OrdinalIgnoreCase)
        );

        Assert.False(
            condition: hasIterationField,
            userMessage: "PROV-H06 regression: State machine should NOT contain an 'iteration' field. "
                         + "The contradictory while loop and its iteration variable should be removed. "
                         + $"Fields found: [{string.Join(separator: ", ", values: allFields.Select(selector: f => $"{f.Name}: {f.FieldType.Name}"))}]"
        );
    }

    [Fact]
    public void Get_Method_IsAsync_ReturnsGenericTask()
    {
        MethodInfo? getMethod = typeof(AcoustIdBaseClient).GetMethod(
            name: "Get",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: getMethod);
        Assert.True(condition: getMethod.ReturnType.IsGenericType);
        Assert.Equal(expected: typeof(Task<>), actual: getMethod.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void Get_Method_HasAsyncStateMachineAttribute()
    {
        MethodInfo? getMethod = typeof(AcoustIdBaseClient).GetMethod(
            name: "Get",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: getMethod);

        AsyncStateMachineAttribute? attr =
            getMethod.GetCustomAttribute<AsyncStateMachineAttribute>();

        Assert.NotNull(@object: attr);
    }

    [Fact]
    public void Get_Method_HasCorrectParameters()
    {
        MethodInfo? getMethod = typeof(AcoustIdBaseClient).GetMethod(
            name: "Get",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: getMethod);

        ParameterInfo[] parameters = getMethod.GetParameters();
        Assert.Equal(expected: 4, actual: parameters.Length);
        Assert.Equal(expected: "url", actual: parameters[0].Name);
        Assert.Equal(expected: "query", actual: parameters[1].Name);
        Assert.Equal(expected: "priority", actual: parameters[2].Name);
        Assert.Equal(expected: "skipCache", actual: parameters[3].Name);
    }

    [Fact]
    public void Get_Method_StateMachine_HasNoWhileLoopFields()
    {
        // A while loop with `iteration < 10` would require the compiler to store
        // the iteration counter as a field in the async state machine.
        // After removing the while loop, verify no such counter field exists.
        MethodInfo? getMethod = typeof(AcoustIdBaseClient).GetMethod(
            name: "Get",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(@object: getMethod);

        Type? stateMachineType = getMethod
            .GetCustomAttribute<AsyncStateMachineAttribute>()
            ?.StateMachineType;
        Assert.NotNull(@object: stateMachineType);

        FieldInfo[] fields = stateMachineType.GetFields(
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
        );

        // The state machine should have fields for: url, query, priority, retry,
        // data, response, newUrl, result, and various awaiter/builder fields.
        // It should NOT have a field for an iteration counter.
        // Compiler typically generates fields like <>5__1, <>5__2 for locals.
        // We check that no int-typed generated field corresponds to `iteration`.

        // Filter to user-code local variable fields (compiler-generated with 5__ pattern)
        FieldInfo[] localFields = fields.Where(predicate: f => f.Name.Contains(value: "5__")).ToArray();

        // Count int-type locals — removing the while loop should reduce this count.
        // The original code had `int iteration = 0;` as the only int local in the try block.
        // After removal, no int locals should exist in the try block portion.
        // (retry parameter is a state machine field, not a 5__ local)
        bool hasIterationLikeField = localFields.Any(predicate: f =>
            f.Name.Contains(value: "iteration", comparisonType: StringComparison.OrdinalIgnoreCase)
        );

        Assert.False(
            condition: hasIterationLikeField,
            userMessage: "PROV-H06 regression: No 'iteration' local variable should exist in the state machine "
                         + "after removing the contradictory while loop."
        );
    }

    [Fact]
    public void AcoustIdBaseClient_Implements_IDisposable()
    {
        Assert.True(condition: typeof(IDisposable).IsAssignableFrom(c: typeof(AcoustIdBaseClient)));
    }
}
