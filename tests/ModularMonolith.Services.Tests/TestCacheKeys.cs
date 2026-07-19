using System;
using NSubstitute.Core;
using SharedKernel.Caching;

namespace ModularMonolith.Services.Tests;

internal static class TestCacheKeys
{
    public static CacheKey FromComposeCall(CallInfo callInfo)
    {
        ArgumentNullException.ThrowIfNull(callInfo);

        return new CacheKey(
            "test",
            "app",
            callInfo.ArgAt<string>(0) ?? throw new InvalidOperationException("Compose module argument was null."),
            callInfo.ArgAt<string>(1) ?? throw new InvalidOperationException("Compose entity argument was null."),
            callInfo.ArgAt<string>(2) ?? throw new InvalidOperationException("Compose version argument was null."),
            null,
            null,
            null,
            callInfo.ArgAt<string>(3) ?? throw new InvalidOperationException("Compose discriminator argument was null."));
    }
}
