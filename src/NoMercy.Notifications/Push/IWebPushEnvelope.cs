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

public interface IWebPushEnvelope
{
    /// <summary>
    /// Seals <paramref name="plaintext"/> per RFC 8291 into an <c>aes128gcm</c> body
    /// (RFC 8188) that only the holder of the device's private key can open.
    /// Returns the RAW binary body, never base64. The caller decides the wire
    /// encoding: base64 it before putting it in a JSON field to the relay (the
    /// relay decodes it back to raw bytes before writing the Web Push request
    /// body), or hand the base64 string straight to an FCM data message, whose
    /// string values the client decodes on the other end.
    /// </summary>
    byte[] Seal(byte[] plaintext, string p256dhBase64Url, string authBase64Url);
}
