using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Pidgin.ParseStates;

namespace Pidgin
{
    public static partial class Parser<TToken>
    {
        /// <summary>
        /// Creates a parser that parses and returns a literal sequence of tokens
        /// </summary>
        /// <param name="tokens">A sequence of tokens</param>
        /// <returns>A parser that parses a literal sequence of tokens</returns>
        public static Parser<TToken, TToken[]> Sequence(params TToken[] tokens)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            return Sequence<TToken[]>(tokens);
        }
        /// <summary>
        /// Creates a parser that parses and returns a literal sequence of tokens.
        /// The input enumerable is enumerated and copied to a list.
        /// </summary>
        /// <typeparam name="TEnumerable">The type of tokens to parse</typeparam>
        /// <param name="tokens">A sequence of tokens</param>
        /// <returns>A parser that parses a literal sequence of tokens</returns>
        public static Parser<TToken, TEnumerable> Sequence<TEnumerable>(TEnumerable tokens)
            where TEnumerable : IEnumerable<TToken>
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            return new SequenceTokenParser<TEnumerable>(tokens);
        }

        private sealed class SequenceTokenParser<TEnumerable> : Parser<TToken, TEnumerable>
            where TEnumerable : IEnumerable<TToken>
        {
            private readonly TEnumerable _value;
            private readonly TToken[] _valueTokens;

            public SequenceTokenParser(TEnumerable value)
                : base(ImmutableSortedSet.Create(new Expected<TToken>(Rope.CreateRange(value))))
            {
                _value = value;
                _valueTokens = value.ToArray();
            }

            internal sealed override InternalResult<TEnumerable> Parse(IParseState<TToken> state)
            {
                var consumedInput = false;
                foreach (var x in _valueTokens)
                {
                    var result = state.Peek();
                    if (!result.HasValue)
                    {
                        state.Error = new ParseError<TToken>(
                            result,
                            true,
                            Expected,
                            state.SourcePos,
                            null
                        );
                        return InternalResult.Failure<TEnumerable>(consumedInput);
                    }

                    TToken token = result.GetValueOrDefault();
                    if (!EqualityComparer<TToken>.Default.Equals(token, x))
                    {
                        state.Error = new ParseError<TToken>(
                            result,
                            false,
                            Expected,
                            state.SourcePos,
                            null
                        );
                        return InternalResult.Failure<TEnumerable>(consumedInput);
                    }

                    consumedInput = true;
                    state.Advance();
                }
                return InternalResult.Success<TEnumerable>(_value, consumedInput);
            }
        }

        /// <summary>
        /// Creates a parser that applies a sequence of parsers and collects the results.
        /// This parser fails if any of its constituent parsers fail
        /// </summary>
        /// <typeparam name="T">The return type of the parsers</typeparam>
        /// <param name="parsers">A sequence of parsers</param>
        /// <returns>A parser that applies a sequence of parsers and collects the results</returns>
        public static Parser<TToken, IEnumerable<T>> Sequence<T>(params Parser<TToken, T>[] parsers)
        {
            return Sequence(parsers.AsEnumerable());
        }

        /// <summary>
        /// Creates a parser that applies a sequence of parsers and collects the results.
        /// This parser fails if any of its constituent parsers fail
        /// </summary>
        /// <typeparam name="T">The return type of the parsers</typeparam>
        /// <param name="parsers">A sequence of parsers</param>
        /// <returns>A parser that applies a sequence of parsers and collects the results</returns>
        public static Parser<TToken, IEnumerable<T>> Sequence<T>(IEnumerable<Parser<TToken, T>> parsers)
        {
            if (parsers == null)
            {
                throw new ArgumentNullException(nameof(parsers));
            }
            var parsersArray = parsers.ToArray();
            if (parsersArray.Length == 1)
            {
                return parsersArray[0].Select(x => new[] { x }.AsEnumerable());
            }
            return new SequenceParser<T>(parsersArray);
        }

        private sealed class SequenceParser<T> : Parser<TToken, IEnumerable<T>>
        {
            private readonly Parser<TToken, T>[] _parsers;

            public SequenceParser(Parser<TToken, T>[] parsers) : base(ExpectedUtil.Concat(parsers.Select(p => p.Expected)))
            {
                _parsers = parsers;
            }

            internal sealed override InternalResult<IEnumerable<T>> Parse(IParseState<TToken> state)
            {
                var consumedInput = false;
                var ts = new T[_parsers.Length];
                
                for (var i = 0; i < _parsers.Length; i++)
                {
                    var p = _parsers[i];
                
                    var result = p.Parse(state);
                    consumedInput = consumedInput || result.ConsumedInput;
                
                    if (!result.Success)
                    {
                        state.Error = state.Error.WithExpected(Expected);
                        return InternalResult.Failure<IEnumerable<T>>(consumedInput);
                    }
                
                    ts[i] = result.Value;
                }

                return InternalResult.Success<IEnumerable<T>>(ts, consumedInput);
            }
        }
    }
}