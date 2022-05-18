using System;
using Nocturo.Common.Utilities;
using System.Runtime.CompilerServices;

namespace Nocturo.Downloader.Utilities
{
    internal static unsafe class Util
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string GetAnsiStringFromByteSpan(ReadOnlySpan<byte> input) 
            => GetAnsiStringFromByteSpan(input, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string GetAnsiStringFromByteSpan(ReadOnlySpan<byte> input, int index)
        {
            /*
             * Why do we do that instead of using Utf8JsonReader::GetString? Well, when parsing data groups, we need to get rid of the trailing zero.
             * By default, GetString will convert the Utf8JsonReader::ValueSpan to a string from index 0. In that case we will have to call Substring(1) afterwards
             * to achieve what we need which is bad because we're creating 2 string objects for each data group (strings are immutable, 
             * any operation on them returns a new object). Therefore by using the string constructor that takes a pointer as argument, we are able to choose 
             * which part of the input array to convert.
             */
            if (input.IsEmpty)
                return string.Empty;

            if (index < 0)
                throw new ArgumentException("Index value cannot be negative", nameof(index));

            fixed (byte* pInput = input)
                return new((sbyte*)pInput, index, input.Length - index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TValue ReinterpretCast<TValue>(nint ptr) where TValue : unmanaged 
            => ptr != IntPtr.Zero ? *(TValue*)ptr : throw new ArgumentNullException(nameof(ptr));
    }
}