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
using System.Collections.Generic;

namespace PdfClown.Documents.Functions.Type4
{
    /**
     * Makes up the execution context, holding the available operators and the execution stack.
     *
     */
    public class ExecutionContext
    {

        private readonly Stack<PdfDirectObject> stack = new();

        /**
         * Creates a new execution context.
         * @param operatorSet the operator set
         */
        public ExecutionContext()
        {
        }

        /**
         * Returns the stack used by this execution context.
         * @return the stack
         */
        public Stack<PdfDirectObject> Stack
        {
            get => this.stack;
        }

        /**
         * Pops a number (int or real) from the stack. If it's neither data type, a
         * ClassCastException is thrown.
         * @return the number
         */
        public IPdfNumber PopNumber()
        {
            return (IPdfNumber)stack.Pop();
        }

        /**
         * Pops a value of type int from the stack. If the value is not of type int, a
         * ClassCastException is thrown.
         * @return the int value
         */
        public int PopInt()
        {
            return ((IPdfNumber)stack.Pop()).IntValue;
        }

        /**
         * Pops a number from the stack and returns it as a real value. If the value is not of a
         * numeric type, a ClassCastException is thrown.
         * @return the real value
         */
        public float PopReal()
        {
            return ((IPdfNumber)stack.Pop()).FloatValue;
        }

        public bool PopBool()
        {
            return ((PdfBoolean)stack.Pop()).BooleanValue;
        }

        public InstructionSequence PopInstruction()
        {
            return (InstructionSequence)stack.Pop();
        }

        public void Push(float value)
        {
            stack.Push(new PdfReal(value));
        }

        public void Push(double value)
        {
            stack.Push(new PdfReal(value));
        }

        public void Push(int value)
        {
            stack.Push(new PdfInteger(value));
        }

        public void Push(bool value)
        {
            stack.Push(PdfBoolean.Get(value));
        }


    }
}
