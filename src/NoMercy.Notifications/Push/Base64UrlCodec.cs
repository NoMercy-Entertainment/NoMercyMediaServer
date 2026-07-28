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

namespace NoMercy.Notifications.Push;

/// <summary>
/// Base64url (RFC 4648 §5) codec shared by the envelope and its callers, so a
/// caller holding only <see cref="IWebPushEnvelope"/> — such as the dispatcher —
/// never has to reach through to the concrete <see cref="WebPushEnvelope"/> type
/// just to encode a sealed body or decode a stored key.
/// </summary>
public static class Base64UrlCodec
{
    public static byte[] Decode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);
        return Convert.FromBase64String(padded);
    }

    public static string Encode(byte[] value) =>
        Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
