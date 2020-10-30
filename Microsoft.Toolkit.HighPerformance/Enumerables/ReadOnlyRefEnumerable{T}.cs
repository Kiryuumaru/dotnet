// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
#if SPAN_RUNTIME_SUPPORT
using System.Runtime.InteropServices;
#endif
using Microsoft.Toolkit.HighPerformance.Extensions;
using Microsoft.Toolkit.HighPerformance.Helpers.Internals;
#if !SPAN_RUNTIME_SUPPORT
using RuntimeHelpers = Microsoft.Toolkit.HighPerformance.Helpers.Internals.RuntimeHelpers;
#endif

namespace Microsoft.Toolkit.HighPerformance.Enumerables
{
    /// <summary>
    /// A <see langword="ref"/> <see langword="struct"/> that iterates readonly items from arbitrary memory locations.
    /// </summary>
    /// <typeparam name="T">The type of items to enumerate.</typeparam>
    public ref struct ReadOnlyRefEnumerable<T>
    {
#if SPAN_RUNTIME_SUPPORT
        /// <summary>
        /// The <see cref="ReadOnlySpan{T}"/> instance pointing to the first item in the target memory area.
        /// </summary>
        /// <remarks>The <see cref="ReadOnlySpan{T}.Length"/> field maps to the total available length.</remarks>
        private readonly ReadOnlySpan<T> span;
#else
        /// <summary>
        /// The target <see cref="object"/> instance, if present.
        /// </summary>
        private readonly object? instance;

        /// <summary>
        /// The initial offset within <see cref="instance"/>.
        /// </summary>
        private readonly IntPtr offset;

        /// <summary>
        /// The total available length for the sequence.
        /// </summary>
        private readonly int length;
#endif

        /// <summary>
        /// The distance between items in the sequence to enumerate.
        /// </summary>
        /// <remarks>The distance refers to <typeparamref name="T"/> items, not byte offset.</remarks>
        private readonly int step;

        /// <summary>
        /// The current position in the sequence.
        /// </summary>
        private int position;

#if SPAN_RUNTIME_SUPPORT
        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyRefEnumerable{T}"/> struct.
        /// </summary>
        /// <param name="span">The <see cref="ReadOnlySpan{T}"/> instance pointing to the first item in the target memory area.</param>
        /// <param name="step">The distance between items in the sequence to enumerate.</param>
        /// <param name="position">The current position in the sequence.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlyRefEnumerable(ReadOnlySpan<T> span, int step, int position)
        {
            this.span = span;
            this.step = step;
            this.position = position;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyRefEnumerable{T}"/> struct.
        /// </summary>
        /// <param name="reference">A reference to the first item of the sequence.</param>
        /// <param name="length">The number of items in the sequence.</param>
        /// <param name="step">The distance between items in the sequence to enumerate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyRefEnumerable(in T reference, int length, int step)
        {
            this.span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(reference), length);
            this.step = step;
            this.position = -1;
        }
#else
        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyRefEnumerable{T}"/> struct.
        /// </summary>
        /// <param name="instance">The target <see cref="object"/> instance.</param>
        /// <param name="offset">The initial offset within <see paramref="instance"/>.</param>
        /// <param name="length">The number of items in the sequence.</param>
        /// <param name="step">The distance between items in the sequence to enumerate.</param>
        /// <param name="position">The current position in the sequence.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlyRefEnumerable(object? instance, IntPtr offset, int length, int step, int position)
        {
            this.instance = instance;
            this.offset = offset;
            this.length = length;
            this.step = step;
            this.position = position;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyRefEnumerable{T}"/> struct.
        /// </summary>
        /// <param name="instance">The target <see cref="object"/> instance.</param>
        /// <param name="offset">The initial offset within <see paramref="instance"/>.</param>
        /// <param name="length">The number of items in the sequence.</param>
        /// <param name="step">The distance between items in the sequence to enumerate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyRefEnumerable(object? instance, IntPtr offset, int length, int step)
        {
            this.instance = instance;
            this.offset = offset;
            this.length = length;
            this.step = step;
            this.position = -1;
        }
#endif

        /// <inheritdoc cref="System.Collections.IEnumerable.GetEnumerator"/>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlyRefEnumerable<T> GetEnumerator() => this;

        /// <inheritdoc cref="System.Collections.IEnumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
#if SPAN_RUNTIME_SUPPORT
            return ++this.position < this.span.Length;
#else
            return ++this.position < this.length;
#endif
        }

        /// <inheritdoc cref="System.Collections.Generic.IEnumerator{T}.Current"/>
        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if SPAN_RUNTIME_SUPPORT
                ref T r0 = ref this.span.DangerousGetReference();
#else
                ref T r0 = ref RuntimeHelpers.GetObjectDataAtOffsetOrPointerReference<T>(this.instance, this.offset);
#endif
                nint offset = (nint)(uint)this.position * (nint)(uint)this.step;
                ref T ri = ref Unsafe.Add(ref r0, offset);

                return ref ri;
            }
        }

        /// <summary>
        /// Copies the contents of this <see cref="RefEnumerable{T}"/> into a destination <see cref="Span{T}"/> instance.
        /// </summary>
        /// <param name="destination">The destination <see cref="Span{T}"/> instance.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="destination"/> is shorter than the source <see cref="RefEnumerable{T}"/> instance.
        /// </exception>
        public readonly void CopyTo(Span<T> destination)
        {
#if SPAN_RUNTIME_SUPPORT
            if (this.step == 1)
            {
                this.span.CopyTo(destination);

                return;
            }

            ref T sourceRef = ref this.span.DangerousGetReference();
            int length = this.span.Length;
#else
            ref T sourceRef = ref RuntimeHelpers.GetObjectDataAtOffsetOrPointerReference<T>(this.instance, this.offset);
            int length = this.length;
#endif
            if ((uint)destination.Length < (uint)length)
            {
                ThrowArgumentExceptionForDestinationTooShort();
            }

            ref T destinationRef = ref destination.DangerousGetReference();

            RefEnumerableHelper.CopyTo(ref sourceRef, ref destinationRef, (nint)(uint)length, (nint)(uint)this.step);
        }

        /// <summary>
        /// Attempts to copy the current <see cref="RefEnumerable{T}"/> instance to a destination <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="destination">The target <see cref="Span{T}"/> of the copy operation.</param>
        /// <returns>Whether or not the operation was successful.</returns>
        public readonly bool TryCopyTo(Span<T> destination)
        {
#if SPAN_RUNTIME_SUPPORT
            int length = this.span.Length;
#else
            int length = this.length;
#endif

            if (destination.Length >= length)
            {
                CopyTo(destination);

                return true;
            }

            return false;
        }

        /// <inheritdoc cref="RefEnumerable{T}.ToArray"/>
        [Pure]
        public readonly T[] ToArray()
        {
#if SPAN_RUNTIME_SUPPORT
            int length = this.span.Length;
#else
            int length = this.length;
#endif

            // Empty array if no data is mapped
            if (length == 0)
            {
                return Array.Empty<T>();
            }

            T[] array = new T[length];

            CopyTo(array);

            return array;
        }

        /// <summary>
        /// Implicitly converts a <see cref="RefEnumerable{T}"/> instance into a <see cref="ReadOnlyRefEnumerable{T}"/> one.
        /// </summary>
        /// <param name="enumerable">The input <see cref="RefEnumerable{T}"/> instance.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyRefEnumerable<T>(RefEnumerable<T> enumerable)
        {
#if SPAN_RUNTIME_SUPPORT
            return new ReadOnlyRefEnumerable<T>(enumerable.Span, enumerable.Step, enumerable.Position);
#else
            return new ReadOnlyRefEnumerable<T>(enumerable.Instance, enumerable.Offset, enumerable.Length, enumerable.Position);
#endif
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> when the target span is too short.
        /// </summary>
        private static void ThrowArgumentExceptionForDestinationTooShort()
        {
            throw new ArgumentException("The target span is too short to copy all the current items to");
        }
    }
}
