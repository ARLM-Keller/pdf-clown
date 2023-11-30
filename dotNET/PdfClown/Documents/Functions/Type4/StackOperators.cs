/*
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
using PdfClown.Objects;
using PdfClown.Util.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfClown.Documents.Functions.Type4
{

    /**
     * Provides the stack operators such as "Pop" and "dup".
     *
     */
    internal class StackOperators
    {

        private StackOperators()
        {
            // Private constructor.
        }

        /** Implements the "copy" operator. */
        internal sealed class Copy : Operator
        {
            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                int n = ((IPdfNumber)stack.Pop()).IntValue;
                if (n > 0)
                {
                    int size = stack.Count;
                    //Need to copy to a new list to avoid ConcurrentModificationException
                    var copy = stack.Skip(size - n).ToList();
                    stack.AddRange(copy);
                }
            }
        }

        /** Implements the "dup" operator. */
        internal sealed class Dup : Operator
        {
            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                stack.Push(stack.Peek());
            }

        }

        /** Implements the "exch" operator. */
        internal sealed class Exch : Operator
        {
            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                var any2 = stack.Pop();
                var any1 = stack.Pop();
                stack.Push(any2);
                stack.Push(any1);
            }

        }

        /** Implements the "index" operator. */
        internal sealed class Index : Operator
        {
            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                int n = ((IPdfNumber)stack.Pop()).IntValue;
                if (n < 0)
                {
                    throw new ArgumentException("rangecheck: " + n);
                }
                int size = stack.Count;
                stack.Push(stack.ElementAt(size - n - 1));
            }

        }

        /** Implements the "Pop" operator. */
        internal sealed class Pop : Operator
        {
            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                stack.Pop();
            }
        }

        /** Implements the "roll" operator. */
        internal sealed class Roll : Operator
        {
            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                int j = ((IPdfNumber)stack.Pop()).IntValue;
                int n = ((IPdfNumber)stack.Pop()).IntValue;
                if (j == 0)
                {
                    return; //Nothing to do
                }
                if (n < 0)
                {
                    throw new ArgumentException("rangecheck: " + n);
                }

                var rolled = new LinkedList<PdfDirectObject>();
                var moved = new LinkedList<PdfDirectObject>();
                if (j < 0)
                {
                    //negative roll
                    int n1 = n + j;
                    for (int i = 0; i < n1; i++)
                    {
                        moved.AddFirst(stack.Pop());
                    }
                    for (int i = j; i < 0; i++)
                    {
                        rolled.AddFirst(stack.Pop());
                    }
                    stack.AddRange(moved);
                    stack.AddRange(rolled);
                }
                else
                {
                    //positive roll
                    int n1 = n - j;
                    for (int i = j; i > 0; i--)
                    {
                        rolled.AddFirst(stack.Pop());
                    }
                    for (int i = 0; i < n1; i++)
                    {
                        moved.AddFirst(stack.Pop());
                    }
                    stack.AddRange(rolled);
                    stack.AddRange(moved);
                }
            }
        }
    }
}
