// *********************************************************
//
// Copyright © Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of
// the License at
//
// http://www.apache.org/licenses/LICENSE-2.0 
//
// THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES
// OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED,
// INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES
// OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache 2 License for the specific language
// governing permissions and limitations under the License.
//
// *********************************************************

using System;
using System.Text;

namespace CSharpToVisualBasicConverter.Utilities
{
    internal static class StringExtensions
    {
        public static string Repeat(this string s, int count)
        {
            if (s == null)
            {
                throw new ArgumentNullException("s");
            }

            if (count == 0 || s.Length == 0)
            {
                return string.Empty;
            }
            else if (count == 1)
            {
                return s;
            }
            else
            {
                var builder = new StringBuilder(s.Length * count);
                for (int i = 0; i < count; i++)
                {
                    builder.Append(s);
                }

                return builder.ToString();
            }
        }
    }
}
