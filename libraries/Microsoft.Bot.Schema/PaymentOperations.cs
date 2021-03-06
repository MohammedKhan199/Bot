// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Bot.Schema
{
    /// <summary>
    /// Names for invocable operations in the payment callback protocol.
    /// </summary>
    [Obsolete("Bot Framework no longer supports payments.")]
    public static class PaymentOperations
    {
        /// <summary>
        /// Name for the Update Shipping Address operation invocation.
        /// </summary>
        /// <remarks>
        /// This operation accepts a PaymentRequestUpdate object and returns a PaymentDetails object or an error.
        /// </remarks>
        public const string UpdateShippingAddressOperationName = "payments/update/shippingAddress";

        /// <summary>
        /// Name for the Update Shipping Option operation invocation.
        /// </summary>
        /// <remarks>
        /// This operation accepts a PaymentRequestUpdate object and returns a PaymentDetails object or an error.
        /// </remarks>
        public const string UpdateShippingOptionOperationName = "payments/update/shippingOption";

        /// <summary>
        /// Name for the payment completion operation invocation.
        /// </summary>
        /// <remarks>
        /// This operation accepts a PaymentRequestComplete object and returns a PaymentRequestResult object or an error.
        /// </remarks>
        public const string PaymentCompleteOperationName = "payments/complete";
    }
}
