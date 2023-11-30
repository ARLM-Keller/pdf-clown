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
using System;

namespace PdfClown.Documents.Functions.Type4
{

    /**
     * Provides the bitwise operators such as "and" and "xor".
     *
     */
    public class BitwiseOperators
    {

        private BitwiseOperators()
        {
            // Private constructor.
        }

        /** Abstract base class for logical operators. */
        internal abstract class AbstractLogicalOperator : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                var op2 = stack.Pop();
                var op1 = stack.Pop();
                if (op1 is PdfBoolean op1Bool && op2 is PdfBoolean op2Bool)
                {
                    bool bool1 = op1Bool.BooleanValue;
                    bool bool2 = op2Bool.BooleanValue;
                    bool result = ApplyForBoolean(bool1, bool2);
                    stack.Push(PdfBoolean.Get(result));
                }
                else if (op1 is PdfInteger op1Int && op2 is PdfInteger op2Int)
                {
                    int int1 = op1Int.IntValue;
                    int int2 = op2Int.IntValue;
                    int result = ApplyForInteger(int1, int2);
                    stack.Push(new PdfInteger(result));
                }
                else
                {
                    throw new Exception("Operands must be bool/bool or int/int");
                }
            }


            protected abstract bool ApplyForBoolean(bool bool1, bool bool2);

            protected abstract int ApplyForInteger(int int1, int int2);

        }

        /** Implements the "and" operator. */
        internal sealed class And : AbstractLogicalOperator
        {


            protected override bool ApplyForBoolean(bool bool1, bool bool2)
            {
                return bool1 && bool2;
            }


            protected override int ApplyForInteger(int int1, int int2)
            {
                return int1 & int2;
            }
        }

        /** Implements the "bitshift" operator. */
        internal sealed class Bitshift : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                int shift = ((PdfInteger)stack.Pop()).IntValue;
                int int1 = ((PdfInteger)stack.Pop()).IntValue;
                if (shift < 0)
                {
                    int result = int1 >> Math.Abs(shift);
                    stack.Push(new PdfInteger(result));
                }
                else
                {
                    int result = int1 << shift;
                    stack.Push(new PdfInteger(result));
                }
            }

        }

        /** Implements the "false" operator. */
        internal sealed class False : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                stack.Push(PdfBoolean.Get(false));
            }

        }

        /** Implements the "not" operator. */
        internal sealed class Not : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                var op1 = stack.Pop();
                if (op1 is PdfBoolean pdfBool)
                {
                    bool bool1 = pdfBool.BooleanValue;
                    bool result = !bool1;
                    stack.Push(PdfBoolean.Get(result));
                }
                else if (op1 is PdfInteger pdfInt)
                {
                    int int1 = pdfInt.IntValue;
                    int result = -int1;
                    stack.Push(new PdfInteger(result));
                }
                else
                {
                    throw new Exception("Operand must be bool or int");
                }
            }

        }

        /** Implements the "or" operator. */
        internal sealed class Or : AbstractLogicalOperator
        {
            protected override bool ApplyForBoolean(bool bool1, bool bool2)
            {
                return bool1 || bool2;
            }


            protected override int ApplyForInteger(int int1, int int2)
            {
                return int1 | int2;
            }

        }

        /** Implements the "true" operator. */
        internal sealed class True : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                stack.Push(PdfBoolean.Get(true));
            }

        }

        /** Implements the "xor" operator. */
        internal sealed class Xor : AbstractLogicalOperator
        {


            protected override bool ApplyForBoolean(bool bool1, bool bool2)
            {
                return bool1 ^ bool2;
            }


            protected override int ApplyForInteger(int int1, int int2)
            {
                return int1 ^ int2;
            }

        }
    }
}
