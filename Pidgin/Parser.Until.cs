using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Pidgin
{
    public abstract partial class Parser<TToken, T>
    {
        /// <summary>
        /// Creates a parser which applies this parser zero or more times until <paramref name="terminator"/> succeeds.
        /// Fails if this parser fails or if <paramref name="terminator"/> fails after consuming input.
        /// The return value of <paramref name="terminator"/> is ignored.
        /// </summary>
        /// <typeparam name="U">The return type of <paramref name="terminator"/></typeparam>
        /// <param name="terminator">A parser to parse a terminator</param>
        /// <returns>A parser which applies this parser repeatedly until <paramref name="terminator"/> succeeds</returns>
        public Parser<TToken, IEnumerable<T>> Until<U>(Parser<TToken, U> terminator)
        {
            if (terminator == null)
            {
                throw new ArgumentNullException(nameof(terminator));
            }
            return terminator.Then(ReturnEmptyEnumerable)
                .Or(this.AtLeastOnceUntil(terminator));
        }
        
        /// <summary>
        /// Creates a parser which applies this parser one or more times until <paramref name="terminator"/> succeeds.
        /// Fails if this parser fails or if <paramref name="terminator"/> fails after consuming input.
        /// The return value of <paramref name="terminator"/> is ignored.
        /// </summary>
        /// <param name="terminator">A parser to parse a terminator</param>
        /// <returns>A parser which applies this parser repeatedly until <paramref name="terminator"/> succeeds</returns>
        public Parser<TToken, IEnumerable<T>> AtLeastOnceUntil<U>(Parser<TToken, U> terminator)
        {
            if (terminator == null)
            {
                throw new ArgumentNullException(nameof(terminator));
            }
            return new AtLeastOnceUntilParser<U>(this, terminator, true);
        }

        /// <summary>
        /// Creates a parser which applies this parser zero or more times until <paramref name="terminator"/> succeeds, discarding the results.
        /// This is more efficient than <see cref="Until{U}(Parser{TToken, U})"/> if you don't need the results.
        /// Fails if this parser fails or if <paramref name="terminator"/> fails after consuming input.
        /// The return value of <paramref name="terminator"/> is ignored.
        /// </summary>
        /// <typeparam name="U">The return type of <paramref name="terminator"/></typeparam>
        /// <param name="terminator">A parser to parse a terminator</param>
        /// <returns>A parser which applies this parser repeatedly until <paramref name="terminator"/> succeeds, discarding the results</returns>
        public Parser<TToken, Unit> SkipUntil<U>(Parser<TToken, U> terminator)
        {
            if (terminator == null)
            {
                throw new ArgumentNullException(nameof(terminator));
            }
            return terminator.Then(ReturnUnit)
                .Or(this.SkipAtLeastOnceUntil(terminator));
        }
        
        /// <summary>
        /// Creates a parser which applies this parser one or more times until <paramref name="terminator"/> succeeds, discarding the results.
        /// This is more efficient than <see cref="AtLeastOnceUntil{U}(Parser{TToken, U})"/> if you don't need the results.
        /// Fails if this parser fails or if <paramref name="terminator"/> fails after consuming input.
        /// The return value of <paramref name="terminator"/> is ignored.
        /// </summary>
        /// <param name="terminator">A parser to parse a terminator</param>
        /// <returns>A parser which applies this parser repeatedly until <paramref name="terminator"/> succeeds, discarding the results</returns>
        public Parser<TToken, Unit> SkipAtLeastOnceUntil<U>(Parser<TToken, U> terminator)
        {
            if (terminator == null)
            {
                throw new ArgumentNullException(nameof(terminator));
            }
            return new AtLeastOnceUntilParser<U>(this, terminator, false).Then(ReturnUnit);
        }

        private sealed class AtLeastOnceUntilParser<U> : Parser<TToken, IEnumerable<T>>
        {
            private readonly Parser<TToken, T> _parser;
            private readonly Parser<TToken, U> _terminator;
            private readonly bool _keepResults;

            public AtLeastOnceUntilParser(Parser<TToken, T> parser, Parser<TToken, U> terminator, bool keepResults) : base()
            {
                _parser = parser;
                _terminator = terminator;
                _keepResults = keepResults;
            }

            internal override InternalResult<IEnumerable<T>> Parse(ref ParseState<TToken> state)
            {
                var ts = _keepResults ? new List<T>() : null;

                var firstItemResult = _parser.Parse(ref state);
                if (!firstItemResult.Success)
                {
                    // state.Error set by _parser
                    return InternalResult.Failure<IEnumerable<T>>(firstItemResult.ConsumedInput);
                }
                if (!firstItemResult.ConsumedInput)
                {
                    throw new InvalidOperationException("Until() used with a parser which consumed no input");
                }
                ts?.Add(firstItemResult.Value);

                while (true)
                {
                    var terminatorResult = _terminator.Parse(ref state);
                    if (terminatorResult.Success)
                    {
                        return InternalResult.Success<IEnumerable<T>>(ts, true);
                    }
                    if (terminatorResult.ConsumedInput)
                    {
                        // state.Error set by _terminator
                        return InternalResult.Failure<IEnumerable<T>>(true);
                    }

                    var terminatorExpected = state.Error.InternalExpected;
                    var itemResult = _parser.Parse(ref state);
                    if (!itemResult.Success)
                    {
                        if (!itemResult.ConsumedInput)
                        {
                            state.Error = state.Error.WithExpected(state.Error.InternalExpected.Union(terminatorExpected));
                        }
                        return InternalResult.Failure<IEnumerable<T>>(true);
                    }
                    if (!itemResult.ConsumedInput)
                    {
                        throw new InvalidOperationException("Until() used with a parser which consumed no input");
                    }
                    ts?.Add(itemResult.Value);
                }
            }
        }
    }
}