using System;
using System.Collections.Generic;
using System.Text;

namespace PlainValley.AsyncStream
{
    public static class FluentExtensions
    {
        public static TOutput Then<TInput, TOutput>(this TInput target, Func<TInput, TOutput> transform) 
            => transform(target);
    }
}
