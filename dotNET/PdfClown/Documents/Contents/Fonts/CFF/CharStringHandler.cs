/*
 * https://github.com/apache/pdfbox
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfClown.Documents.Contents.Fonts.Type1
{

    /**
     * A Handler for CharStringCommands.
     *
     * @author Villu Ruusmann
     * @author John Hewson
     * 
     */
    public sealed class CharStringHandler
    {
        /**
		 * Handler for a sequence of CharStringCommands.
		 *
		 * @param sequence of CharStringCommands
		 *
		 */
        public List<float> HandleSequence(List<object> sequence, HandleCommand handleCommand)
        {
            var stack = new List<float>();
            for (int i = 0; i < sequence.Count; i++)
            {
                var obj = sequence[i];
                if (obj is CharStringCommand charStringCommand)
                {
                    List<float> results = handleCommand(stack, charStringCommand);
                    stack.Clear();  
                    stack.AddRange(results);
                }
                else
                {
                    stack.Add(Convert.ToSingle(obj));
                }
            }
            return stack.ToList();
        }

        /**
         * Handler for CharStringCommands.
         *
         * @param numbers a list of numbers
         * @param command the CharStringCommand
         * @return a list of commands. This can be empty but never be null.
         */
        public delegate List<float> HandleCommand(List<float> numbers, CharStringCommand command);
    }
}