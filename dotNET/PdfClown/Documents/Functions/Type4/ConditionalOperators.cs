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
using System.ComponentModel.Design.Serialization;

namespace PdfClown.Documents.Functions.Type4
{
    /**
     * Provides the conditional operators such as "if" and "ifelse".
     *
     */
    internal class ConditionalOperators
    {

        private ConditionalOperators()
        {
            // Private constructor.
        }

        /** Implements the "if" operator. */
        internal sealed class If : Operator
        {
            public override void Execute(ExecutionContext context)
            {
                InstructionSequence proc = context.PopInstruction();
                var condition = context.PopBool();
                if (condition)
                {
                    proc.Execute(context);
                }
            }

        }

        /** Implements the "ifelse" operator. */
        internal sealed class IfElse : Operator
        {

            public override void Execute(ExecutionContext context)
            {
                InstructionSequence proc2 = context.PopInstruction();
                InstructionSequence proc1 = context.PopInstruction();
                var condition = context.PopBool();
                if (condition)
                {
                    proc1.Execute(context);
                }
                else
                {
                    proc2.Execute(context);
                }
            }

        }

    }
}
