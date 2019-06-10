﻿using System;
using System.Text;
using NUnit.Framework;

namespace NewRelic.SystemExtensions.UnitTests
{
	[TestFixture]
	public class StringExtensions
	{
		[Test]
		public void when_maxLength_is_less_than_0_then_throws_exception()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => "foo".TruncateUnicodeStringByLength(-1));
		}

		[Test]
		public void when_null_then_throws_exception()
		{
			Assert.Throws<ArgumentNullException>(() => (null as String).TruncateUnicodeStringByLength(0));
		}

		[TestCase("foo", 4, "foo")]
		[TestCase("foo", 3, "foo")]
		[TestCase("foo", 2, "fo")]
		[TestCase("foo", 0, "")]
		[TestCase("€€€", 3, "€€€")]
		[TestCase("€€€", 2, "€€")]
		public void TruncationByLength(String inputString, Int32 maxLength, String expectedResult)
		{
			var actualResult = inputString.TruncateUnicodeStringByLength(maxLength);

			Assert.AreEqual(expectedResult, actualResult);
		}
		
		[TestCase("abcde♥", (uint)10, "abcde♥")] //"abcde♥" has 8 bytes
		[TestCase("abcde♥", (uint)8, "abcde♥")]  //"abcde♥" has 8 bytes
		[TestCase("abcde♥", (uint)7, "abcde")]   //"abcde♥" has 8 bytes
		[TestCase("abcგთde♥", (uint)8, "abcგ")]  // "abcგთde♥" has 14 bytes
		[TestCase(null, (uint)8, null)]
		[TestCase("", (uint)8, "")]
		[TestCase("asdf", (uint)0, "")]
		[TestCase("asdf", (uint)1, "a")]
		[TestCase("♥asdf", (uint)1, "")]
		public void TruncationByBytes(string inputString, uint maxBytes, string expectedResult)
		{
			var actualResult = inputString.TruncateUnicodeStringByBytes(maxBytes);
			Assert.AreEqual(expectedResult, actualResult);
		}

		[TestCase(null, false, new String[] {})]
		[TestCase(null, false, new[] {"baz"})]
		[TestCase("foo", false, new String[] {})]
		[TestCase("foo", false, new[] {"bar"})]
		[TestCase("foo", true, new[] {"foo"})]
		[TestCase("foo", true, new[] {"FOO"})]
		[TestCase("foo", true, new[] {"foo", "baz"})]
		[TestCase("foobar", true, new[] {"foo"})]
		[TestCase("foobar", true, new[] {"FOO"})]
		[TestCase("foobar", true, new[] {"foo", "baz"})]
		public void ContainsAny_ReturnsTrue_IfSourceStringContainsAnyOfTargetStrings_WhileIgnoringCase(String source, Boolean expectedResult, params String[] searchTargets)
		{
			// ReSharper disable once RedundantArgumentDefaultValue
			var result = source.ContainsAny(searchTargets, StringComparison.InvariantCultureIgnoreCase);

			Assert.AreEqual(expectedResult, result);
		}

		[TestCase(null, false, new String[] { })]
		[TestCase(null, false, new[] { "baz" })]
		[TestCase("foo", false, new String[] { })]
		[TestCase("foo", false, new[] { "bar" })]
		[TestCase("foo", true, new[] { "foo" })]
		[TestCase("foo", false, new[] { "FOO" })]
		[TestCase("foo", true, new[] { "foo", "baz" })]
		[TestCase("foobar", true, new[] { "foo" })]
		[TestCase("foobar", false, new[] { "FOO" })]
		[TestCase("foobar", true, new[] { "foo", "baz" })]
		public void ContainsAny_ReturnsTrue_IfSourceStringContainsAnyOfTargetStrings_WhileRespectingCase(String source, Boolean expectedResult, params String[] searchTargets)
		{
			Assert.AreEqual(expectedResult, source.ContainsAny(searchTargets, StringComparison.InvariantCulture));
		}

		[TestCase("foo bar zip zap", 'z', "foo bar ")]
		[TestCase("foo-bar-baz", '-', "foo")]
		[TestCase("foo☃bar☃baz", '☃', "foo")]
		[TestCase("☃-☃☃-☃☃☃", '-', "☃")]
		[TestCase("foo€bar€baz", '€', "foo")]
		[TestCase("€-€€-€€€", '-', "€")]
		[TestCase("http://www.google.com?query=blah", '?', "http://www.google.com")]
		public void TrimAfterAChar(string source, char token, string expectedResult)
		{
			if (source == null)
				throw new ArgumentNullException("source");

			Assert.AreEqual(expectedResult, source.TrimAfterAChar(token));
		}

		[TestCase("abc123", 'x', 1, "abc123")]
		[TestCase("abc123", '2', 1, "abc123")]
		[TestCase("abc123", '3', 0, "abc123")]
		[TestCase("abc123", '3', -1, "abc123")]
		[TestCase("abc123", '3', 1, "abc12")]
		[TestCase("abc123", '3', 5, "abc12")]
		[TestCase("abc333", '3', 1, "abc33")]
		[TestCase("abc333", '3', 3, "abc")]
		public void TrimEnd(String source, Char trimChar, Int32 maxCharactersToTrim, String expectedResult)
		{
			if (source == null)
				throw new ArgumentNullException("source");

			var actualResult = source.TrimEnd(trimChar, maxCharactersToTrim);

			Assert.AreEqual(expectedResult, actualResult);
		}

		[TestCase("foo", "bar", "barfoo")]
		[TestCase("barfoo", "bar", "barfoo")]
		[TestCase("arfoo", "bar", "bararfoo")]
		[TestCase("foo", null, "foo")]
		public void EnsureLeading(String source, String leading, String expectedResult)
		{
			var actualResult = source.EnsureLeading(leading);

			Assert.AreEqual(expectedResult, actualResult);
		}

		[TestCase("foo", "bar", "foobar")]
		[TestCase("foobar", "bar", "foobar")]
		[TestCase("fooba", "bar", "foobabar")]
		[TestCase("foo", null, "foo")]
		public void EnsureTrailing(String source, String trailing, String expectedResult)
		{
			var actualResult = source.EnsureTrailing(trailing);

			Assert.AreEqual(expectedResult, actualResult);
		}
	}
}