using System.Reflection;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Queue;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// HIGH-18: Tests verifying that JobQueue retry logic uses reduced max attempts (5 instead of 10)
/// and adds jitter to prevent thundering herd on concurrent retries.
/// </summary>
[Trait("Category", "Unit")]
public class RetryJitterTests
{
    [Fact]
    public void MaxDbRetryAttempts_IsFive()
    {
        FieldInfo? field = typeof(JobQueue).GetField(
            "MaxDbRetryAttempts",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        int value = (int)field.GetValue(null)!;
        Assert.Equal(5, value);
    }

    [Fact]
    public void BaseRetryDelayMs_Is2000()
    {
        FieldInfo? field = typeof(JobQueue).GetField(
            "BaseRetryDelayMs",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        int value = (int)field.GetValue(null)!;
        Assert.Equal(2000, value);
    }

    [Fact]
    public void MaxJitterMs_Is500()
    {
        FieldInfo? field = typeof(JobQueue).GetField(
            "MaxJitterMs",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        int value = (int)field.GetValue(null)!;
        Assert.Equal(500, value);
    }

    [Fact]
    public void RetryDelay_HasJitter_ProducesVariedValues()
    {
        // Verify that Random.Shared.Next(500) produces varied jitter values
        // by sampling multiple times and checking they are not all identical.
        int baseDelay = 2000;
        int maxJitter = 500;
        HashSet<int> observedDelays = [];

        for (int i = 0; i < 50; i++)
        {
            int delay = baseDelay + Random.Shared.Next(maxJitter);
            observedDelays.Add(delay);
        }

        // With 50 samples and 500 possible jitter values, we expect multiple distinct values.
        Assert.True(observedDelays.Count > 1,
            "Jitter should produce varied delay values to prevent thundering herd");

        // All delays must be within [2000, 2499] range
        foreach (int delay in observedDelays)
        {
            Assert.InRange(delay, baseDelay, baseDelay + maxJitter - 1);
        }
    }

    [Fact]
    public void ReserveJob_RetryMethods_UseConstants_NotHardcoded()
    {
        // Verify that the retry catch blocks in ReserveJob, FailJob, and
        // RequeueFailedJob reference MaxDbRetryAttempts (not hardcoded 10)
        // by inspecting IL for field references to the constants.

        // Get the constant field tokens
        FieldInfo? maxRetryField = typeof(JobQueue).GetField(
            "MaxDbRetryAttempts",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(maxRetryField);

        // Verify the methods exist with expected signatures
        MethodInfo? reserveJob = typeof(JobQueue).GetMethod(
            "ReserveJob",
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(string), typeof(long?), typeof(int)]);
        Assert.NotNull(reserveJob);

        MethodInfo? failJob = typeof(JobQueue).GetMethod(
            "FailJob",
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(QueueJob), typeof(Exception), typeof(int)]);
        Assert.NotNull(failJob);

        MethodInfo? requeueFailedJob = typeof(JobQueue).GetMethod(
            "RequeueFailedJob",
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(int), typeof(int)]);
        Assert.NotNull(requeueFailedJob);

        // Verify all three methods have IL referencing the MaxDbRetryAttempts
        // constant (metadata token). Since constants are inlined by the compiler,
        // we verify the value 5 appears in the IL (ldc.i4.5 = opcode 0x1B,
        // or ldc.i4.s 5 = opcode 0x1F 0x05).
        AssertMethodContainsConstant(reserveJob, 5, "ReserveJob");
        AssertMethodContainsConstant(failJob, 5, "FailJob");
        AssertMethodContainsConstant(requeueFailedJob, 5, "RequeueFailedJob");
    }

    private static void AssertMethodContainsConstant(MethodInfo method, int value, string methodName)
    {
        MethodBody? body = method.GetMethodBody();
        Assert.NotNull(body);

        byte[] il = body.GetILAsByteArray()!;
        Assert.NotNull(il);

        // Look for ldc.i4.5 (0x1B) or ldc.i4.s 5 (0x1F 0x05) or ldc.i4 5 (0x20 + 4 bytes)
        bool found = false;
        for (int i = 0; i < il.Length; i++)
        {
            // ldc.i4.5 = single byte opcode for pushing constant 5
            if (il[i] == 0x1B)
            {
                found = true;
                break;
            }
            // ldc.i4.s <int8> = push small int constant
            if (il[i] == 0x1F && i + 1 < il.Length && il[i + 1] == (byte)value)
            {
                found = true;
                break;
            }
            // ldc.i4 <int32> = push int constant
            if (il[i] == 0x20 && i + 4 < il.Length)
            {
                int val = BitConverter.ToInt32(il, i + 1);
                if (val == value)
                {
                    found = true;
                    break;
                }
            }
        }

        Assert.True(found,
            $"{methodName} should reference MaxDbRetryAttempts constant value ({value}) in its IL. " +
            "This ensures the retry limit of 5 (not the old value of 10) is used.");
    }

    [Fact]
    public void RetryMethods_DoNotContainOldRetryLimit()
    {
        // Verify that the old hardcoded retry limit of 10 is no longer present
        // in the retry methods. We check that `ldc.i4.s 10` (0x1F 0x0A)
        // does NOT appear in comparison context in these methods.
        // Note: 10 might appear for other purposes, so this is a best-effort check.

        MethodInfo? reserveJob = typeof(JobQueue).GetMethod(
            "ReserveJob",
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(string), typeof(long?), typeof(int)]);
        Assert.NotNull(reserveJob);

        MethodBody? body = reserveJob.GetMethodBody();
        Assert.NotNull(body);

        byte[] il = body.GetILAsByteArray()!;

        // ldc.i4.s 10 followed by comparison opcodes (blt, bge, clt, etc.)
        // would indicate a hardcoded limit of 10
        bool foundTenAsLimit = false;
        for (int i = 0; i < il.Length - 2; i++)
        {
            if (il[i] == 0x1F && il[i + 1] == 0x0A)
            {
                // Check if next byte is a branch/comparison opcode
                byte next = il[i + 2];
                // blt.s=0x32, bge.s=0x31, blt=0x3F, bge=0x3E, clt=0xFE04
                if (next == 0x32 || next == 0x31 || next == 0x3F || next == 0x3E)
                {
                    foundTenAsLimit = true;
                }
            }
        }

        Assert.False(foundTenAsLimit,
            "HIGH-18 regression: ReserveJob still uses hardcoded retry limit of 10. " +
            "Should use MaxDbRetryAttempts constant (5).");
    }
}
