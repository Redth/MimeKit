﻿//
// QuotedPrintableDecoder.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2012 Jeffrey Stedfast
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;

namespace MimeKit {
	public class QuotedPrintableDecoder : IMimeDecoder
	{
		enum QpDecoderState : byte {
			PassThrough,
			EqualSign,
			DecodeByte
		}

		QpDecoderState state;
		byte saved;

		/// <summary>
		/// Initializes a new instance of the <see cref="MimeKit.QuotedPrintableDecoder"/> class.
		/// </summary>
		public QuotedPrintableDecoder ()
		{
			Reset ();
		}

		/// <summary>
		/// Clones the decoder.
		/// </summary>
		public object Clone ()
		{
			return MemberwiseClone ();
		}

		/// <summary>
		/// Gets the encoding.
		/// </summary>
		/// <value>
		/// The encoding.
		/// </value>
		public ContentEncoding Encoding {
			get { return ContentEncoding.Base64; }
		}

		/// <summary>
		/// Estimates the length of the output.
		/// </summary>
		/// <returns>
		/// The estimated output length.
		/// </returns>
		/// <param name='inputLength'>
		/// The input length.
		/// </param>
		public int EstimateOutputLength (int inputLength)
		{
			// add an extra 3 bytes for the saved input byte from previous decode step (in case it is invalid hex)
			return inputLength + 3;
		}

		void ValidateArguments (byte[] input, int startIndex, int length, byte[] output)
		{
			if (input == null)
				throw new ArgumentNullException ("input");

			if (startIndex < 0 || startIndex > input.Length)
				throw new ArgumentOutOfRangeException ("startIndex");

			if (length < 0 || startIndex + length > input.Length)
				throw new ArgumentOutOfRangeException ("length");

			if (output == null)
				throw new ArgumentNullException ("output");

			if (output.Length < EstimateOutputLength (length))
				throw new ArgumentException ("The output buffer is not large enough to contain the decoded input.", "output");
		}

		unsafe int UnsafeDecode (byte* input, int length, byte* output)
		{
			byte* inend = input + length;
			byte* outptr = output;
			byte* inptr = input;
			byte c;

			while (inptr < inend) {
				switch (state) {
				case QpDecoderState.PassThrough:
					while (inptr < inend) {
						c = *inptr++;

						if (c == '=') {
							state = QpDecoderState.EqualSign;
							break;
						}

						*outptr++ = c;
					}
					break;
				case QpDecoderState.EqualSign:
					c = *inptr++;
					if (c == '\n') {
						// this is a soft break ("=\n")
						state = QpDecoderState.PassThrough;
					} else {
						state = QpDecoderState.DecodeByte;
						saved = c;
					}
					break;
				case QpDecoderState.DecodeByte:
					c = *inptr++;
					if (c.IsXDigit () && saved.IsXDigit ()) {
						saved = saved.ToXDigit ();
						c = c.ToXDigit ();

						*outptr++ = (byte) ((saved << 4) | c);
					} else if (saved == '\r' && c == '\n') {
						// end-of-line
					} else {
						// invalid encoded sequence - pass it through undecoded
						*outptr++ = (byte) '=';
						*outptr++ = saved;
						*outptr++ = c;
					}

					state = QpDecoderState.PassThrough;
					break;
				}
			}

			return (int) (inptr - output);
		}

		/// <summary>
		/// Decodes the specified input into the output buffer.
		/// </summary>
		/// <returns>
		/// The number of bytes written to the output buffer.
		/// </returns>
		/// <param name='input'>
		/// The input buffer.
		/// </param>
		/// <param name='startIndex'>
		/// The starting index of the input buffer.
		/// </param>
		/// <param name='length'>
		/// The length of the input buffer.
		/// </param>
		/// <param name='output'>
		/// The output buffer.
		/// </param>
		public int Decode (byte[] input, int startIndex, int length, byte[] output)
		{
			ValidateArguments (input, startIndex, length, output);

			unsafe {
				fixed (byte* inptr = input, outptr = output) {
					return UnsafeDecode (inptr + startIndex, length, outptr);
				}
			}
		}

		/// <summary>
		/// Resets the decoder.
		/// </summary>
		public void Reset ()
		{
			state = QpDecoderState.PassThrough;
			saved = 0;
		}
	}
}
