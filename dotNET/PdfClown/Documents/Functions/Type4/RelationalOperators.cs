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

namespace PdfClown.Documents.Functions.Type4
{

    /**
     * Provides the relational operators such as "eq" and "le".
     *
     */
    internal class RelationalOperators
    {

        private RelationalOperators()
        {
            // Private constructor.
        }

        /** Implements the "eq" operator. */
        internal class Eq : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                var op2 = stack.Pop();
                var op1 = stack.Pop();
                bool result = IsEqual(op1, op2);
                context.Push(result);
            }

            protected virtual bool IsEqual(PdfDirectObject op1, PdfDirectObject op2)
            {
                bool result;
                if (op1 is IPdfNumber num1 && op2 is IPdfNumber num2)
                {
                    result = num1.FloatValue.CompareTo(num2.FloatValue) == 0;
                }
                else
                {
                    result = op1.Equals(op2);
                }
                return result;
            }

        }

        /** Abstract base class for number comparison operators. */
        internal abstract class AbstractNumberComparisonOperator : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                var stack = context.Stack;
                var op2 = stack.Pop();
                var op1 = stack.Pop();
                IPdfNumber num1 = (IPdfNumber)op1;
                IPdfNumber num2 = (IPdfNumber)op2;
                bool result = Compare(num1, num2);
                context.Push(result);
            }

            protected abstract bool Compare(IPdfNumber num1, IPdfNumber num2);

        }

        /** Implements the "ge" operator. */
        internal sealed class Ge : AbstractNumberComparisonOperator
        {
            protected override bool Compare(IPdfNumber num1, IPdfNumber num2)
            {
                return num1.FloatValue >= num2.FloatValue;
            }

        }

        /** Implements the "gt" operator. */
        internal sealed class Gt : AbstractNumberComparisonOperator
        {
            protected override bool Compare(IPdfNumber num1, IPdfNumber num2)
            {
                return num1.FloatValue > num2.FloatValue;
            }

        }

        /** Implements the "le" operator. */
        internal sealed class Le : AbstractNumberComparisonOperator
        {
            protected override bool Compare(IPdfNumber num1, IPdfNumber num2)
            {
                return num1.FloatValue <= num2.FloatValue;
            }

        }

        /** Implements the "lt" operator. */
        internal sealed class Lt : AbstractNumberComparisonOperator
        {
            protected override bool Compare(IPdfNumber num1, IPdfNumber num2)
            {
                return num1.FloatValue < num2.FloatValue;
            }

        }

        /** Implements the "ne" operator. */
        internal sealed class Ne : Eq
        {
            protected override bool IsEqual(PdfDirectObject op1, PdfDirectObject op2)
            {
                bool result = base.IsEqual(op1, op2);
                return !result;
            }

        }
    }
}
