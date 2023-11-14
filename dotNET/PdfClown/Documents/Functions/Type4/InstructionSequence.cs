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
using PdfClown.Bytes;
using PdfClown.Files;
using PdfClown.Objects;
using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Functions.Type4
{

    /**
	 * Represents an instruction sequence, a combination of values, operands and nested procedures.
	 *
	 */
    public class InstructionSequence : PdfSimpleObject<List<PdfDirectObject>>
    {

        private readonly List<PdfDirectObject> instructions = new();

        /**
         * Add a name (ex. an operator)
         * @param name the name
         */
        public void AddName(string name)
        {
            this.instructions.Add(new PdfName(name));
        }

        /**
         * Adds an int value.
         * @param value the value
         */
        public void AddInteger(int value)
        {
            this.instructions.Add(new PdfInteger(value));
        }

        /**
         * Adds a real value.
         * @param value the value
         */
        public void AddReal(float value)
        {
            this.instructions.Add(new PdfReal(value));
        }

        /**
         * Adds a bool value.
         * @param value the value
         */
        public void AddBoolean(bool value)
        {
            this.instructions.Add(PdfBoolean.Get(value));
        }

        /**
         * Adds a proc (sub-sequence of instructions).
         * @param child the child proc
         */
        public void AddProc(InstructionSequence child)
        {
            this.instructions.Add(child);
        }

        /**
         * Executes the instruction sequence.
         * @param context the execution context
         */
        public void Execute(ExecutionContext context)
        {
            var stack = context.Stack;
            foreach (PdfDirectObject o in instructions)
            {
                if (o is IPdfString pdfString)
                {
                    string name = pdfString.StringValue;
                    var cmd = Operators.GetOperator(name);
                    if (cmd != null)
                    {
                        cmd.Execute(context);
                    }
                    else
                    {
                        throw new NotSupportedException("Unknown operator or name: " + name);
                    }
                }
                else
                {
                    stack.Push(o);
                }
            }

            //Handles top-level procs that simply need to be executed
            while (stack.Count > 0 && stack.Peek() is InstructionSequence)
            {
                var nested = (InstructionSequence)stack.Pop();
                nested.Execute(context);
            }
        }

        public override int CompareTo(PdfDirectObject obj)
        {
            throw new NotImplementedException();
        }

        public override void WriteTo(IOutputStream stream, File context)
        {
            throw new NotImplementedException();
        }

        public override PdfObject Accept(IVisitor visitor, object data)
        {
            throw new NotImplementedException();
        }
    }
}
