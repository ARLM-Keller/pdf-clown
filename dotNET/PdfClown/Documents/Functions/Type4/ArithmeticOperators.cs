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
using PdfClown.Util.Math;
using System;

namespace PdfClown.Documents.Functions.Type4
{
    /**
	 * Provides the arithmetic operators such as "add" and "sub".
	 *
	 */
    public class ArithmeticOperators
    {

        private ArithmeticOperators()
        {
            // Private constructor.
        }

        /** Implements the "Abs" operator. */
        internal sealed class Abs : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var num = context.PopNumber();
                if (num is PdfInteger)
                {
                    context.Push(Math.Abs(num.IntValue));
                }
                else
                {
                    context.Push(Math.Abs(num.FloatValue));
                }
            }

        }

        /** Implements the "add" operator. */
        internal sealed class Add : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num2 = context.PopNumber();
                IPdfNumber num1 = context.PopNumber();
                if (num1 is PdfInteger int1 && num2 is PdfInteger int2)
                {
                    long sum = int1.LongValue + int2.LongValue;
                    if (sum < int.MinValue || sum > int.MaxValue)
                    {
                        context.Push((float)sum);
                    }
                    else
                    {
                        context.Push((int)sum);
                    }
                }
                else
                {
                    float sum = num1.FloatValue + num2.FloatValue;
                    context.Push(sum);
                }
            }

        }

        /** Implements the "atan" operator. */
        internal sealed class Atan : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                float den = context.PopReal();
                float num = context.PopReal();
                float atan = (float)Math.Atan2(num, den);
                atan = (float)MathUtils.ToRadians(atan) % 360;
                if (atan < 0)
                {
                    atan = atan + 360;
                }
                context.Push(atan);
            }

        }

        /** Implements the "ceiling" operator. */
        internal sealed class Ceiling : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                if (num is PdfInteger integer)
                {
                    context.Stack.Push(integer);
                }
                else
                {
                    context.Stack.Push(new PdfReal((float)Math.Ceiling(num.DoubleValue)));
                }
            }

        }

        /** Implements the "cos" operator. */
        internal sealed class Cos : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                float angle = context.PopReal();
                float cos = (float)Math.Cos(MathUtils.ToRadians(angle));
                context.Push(cos);
            }

        }

        /** Implements the "cvi" operator. */
        internal sealed class Cvi : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                context.Push(num.IntValue);
            }

        }

        /** Implements the "cvr" operator. */
        internal sealed class Cvr : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                context.Push(num.FloatValue);
            }

        }

        /** Implements the "div" operator. */
        internal sealed class Div : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num2 = context.PopNumber();
                IPdfNumber num1 = context.PopNumber();
                context.Push(num1.FloatValue / num2.FloatValue);
            }

        }

        /** Implements the "exp" operator. */
        internal sealed class Exp : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber exp = context.PopNumber();
                IPdfNumber @base = context.PopNumber();
                double value = Math.Pow(@base.DoubleValue, exp.DoubleValue);
                context.Push((float)value);
            }

        }

        /** Implements the "Floor" operator. */
        internal sealed class Floor : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                if (num is PdfInteger num1)
                {
                    context.Stack.Push(num1);
                }
                else
                {
                    context.Push((float)Math.Floor(num.DoubleValue));
                }
            }

        }

        /** Implements the "idiv" operator. */
        internal sealed class IDiv : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                int num2 = context.PopInt();
                int num1 = context.PopInt();
                context.Push(num1 / num2);
            }

        }

        /** Implements the "ln" operator. */
        internal sealed class Ln : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                context.Push(Math.Log(num.DoubleValue));
            }

        }

        /** Implements the "log" operator. */
        internal sealed class Log : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                context.Push(Math.Log10(num.DoubleValue));
            }

        }

        /** Implements the "mod" operator. */
        internal sealed class Mod : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                int int2 = context.PopInt();
                int int1 = context.PopInt();
                context.Push(int1 % int2);
            }

        }

        /** Implements the "mul" operator. */
        internal sealed class Mul : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num2 = context.PopNumber();
                IPdfNumber num1 = context.PopNumber();
                if (num1 is PdfInteger int1 && num2 is PdfInteger int2)
                {
                    long result = int1.LongValue * int2.LongValue;
                    if (result >= int.MinValue && result <= int.MaxValue)
                    {
                        context.Push((int)result);
                    }
                    else
                    {
                        context.Push(result);
                    }
                }
                else
                {
                    double result = num1.DoubleValue * num2.DoubleValue;
                    context.Push(result);
                }
            }

        }

        /** Implements the "neg" operator. */
        internal sealed class Neg : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                if (num is PdfInteger)
                {
                    int v = num.IntValue;
                    if (v == int.MinValue)
                    {
                        context.Push(-num.DoubleValue);
                    }
                    else
                    {
                        context.Push(-num.IntValue);
                    }
                }
                else
                {
                    context.Push(-num.FloatValue);
                }
            }

        }

        /** Implements the "round" operator. */
        internal sealed class Round : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                if (num is PdfInteger int1)
                {
                    context.Stack.Push(int1);
                }
                else
                {
                    context.Push((float)Math.Round(num.DoubleValue));
                }
            }

        }

        /** Implements the "sin" operator. */
        internal sealed class Sin : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                float angle = context.PopReal();
                float sin = (float)Math.Sin(MathUtils.ToRadians(angle));
                context.Push(sin);
            }

        }

        /** Implements the "sqrt" operator. */
        internal sealed class Sqrt : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                float num = context.PopReal();
                if (num < 0)
                {
                    throw new ArgumentException("argument must be nonnegative");
                }
                context.Push((float)Math.Sqrt(num));
            }

        }

        /** Implements the "sub" operator. */
        internal sealed class Sub : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                IPdfNumber num2 = context.PopNumber();
                IPdfNumber num1 = context.PopNumber();
                if (num1 is PdfInteger int1 && num2 is PdfInteger int2)
                {
                    long result = int1.LongValue - int2.LongValue;
                    if (result < int.MinValue || result > int.MaxValue)
                    {
                        stack.Push(new PdfReal(result));
                    }
                    else
                    {
                        stack.Push(new PdfInteger((int)result));
                    }
                }
                else
                {
                    float result = num1.FloatValue - num2.FloatValue;
                    stack.Push(new PdfReal(result));
                }
            }

        }

        /** Implements the "truncate" operator. */
        internal sealed class Truncate : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                IPdfNumber num = context.PopNumber();
                if (num is PdfInteger int1)
                {
                    context.Stack.Push(int1);
                }
                else
                {
                    context.Stack.Push(new PdfReal((int)num.FloatValue));
                }
            }

        }

    }
}
