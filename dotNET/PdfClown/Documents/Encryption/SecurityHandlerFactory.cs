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

using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Encryption
{
    /**
     * Manages security handlers for the application.
     * It follows the singleton pattern.
     * To be usable, security managers must be registered in it.
     * Security managers are retrieved by the application when necessary.
     *
     * @author Benoit Guillon
     * @author John Hewson
     */
    public sealed class SecurityHandlerFactory
    {
        /** Singleton instance */
        public static readonly SecurityHandlerFactory INSTANCE = new SecurityHandlerFactory();

        private readonly Dictionary<string, Type> nameToHandler = new Dictionary<string, Type>(StringComparer.Ordinal);

        private readonly Dictionary<Type, Type> policyToHandler = new Dictionary<Type, Type>();

        private SecurityHandlerFactory()
        {
            RegisterHandler(StandardSecurityHandler.FILTER,
                            typeof(StandardSecurityHandler),
                        typeof(StandardProtectionPolicy));

            RegisterHandler(PublicKeySecurityHandler.FILTER,
                            typeof(PublicKeySecurityHandler),
                            typeof(PublicKeyProtectionPolicy));
        }

        /**
		 * Registers a security handler.
		 *
		 * If the security handler was already registered an exception is thrown.
		 * If another handler was previously registered for the same filter name or
		 * for the same policy name, an exception is thrown
		 *
		 * @param name the name of the filter
		 * @param securityHandler security handler class to register
		 * @param protectionPolicy protection policy class to register
		 */
        public void RegisterHandler(string name, Type securityHandler, Type protectionPolicy)
        {
            if (nameToHandler.ContainsKey(name))
            {
                throw new ArgumentException("The security handler name is already registered");
            }

            nameToHandler[name] = securityHandler;
            policyToHandler[protectionPolicy] = securityHandler;
        }

        /**
		 * Returns a new security handler for the given protection policy, or null none is available.
		 * @param policy the protection policy for which to create a security handler
		 * @return a new SecurityHandler instance, or null if none is available
		 */
        public SecurityHandler NewSecurityHandlerForPolicy(ProtectionPolicy policy)
        {
            if (!policyToHandler.TryGetValue(policy.GetType(), out Type handlerClass))
            {
                return null;
            }

            Type[] argsClasses = { policy.GetType() };
            object[] args = { policy };
            return NewSecurityHandler(handlerClass, argsClasses, args);
        }

        /**
		 * Returns a new security handler for the given Filter name, or null none is available.
		 * @param name the Filter name from the PDF encryption dictionary
		 * @return a new SecurityHandler instance, or null if none is available
		 */
        public SecurityHandler NewSecurityHandlerForFilter(string name)
        {
            if (!nameToHandler.TryGetValue(name, out Type handlerClass))
            {
                return null;
            }

            Type[] argsClasses = { };
            object[] args = { };
            return NewSecurityHandler(handlerClass, argsClasses, args);
        }

        /* Returns a new security handler for the given parameters, or null none is available.
		 *
		 * @param handlerClass the handler class.
		 * @param argsClasses the parameter array.
		 * @param args array of objects to be passed as arguments to the constructor call.
		 * @return a new SecurityHandler instance, or null if none is available.
		 */
        private SecurityHandler NewSecurityHandler(Type handlerClass, Type[] argsClasses, object[] args)
        {
            try
            {
                return (SecurityHandler)Activator.CreateInstance(handlerClass, args);
            }
            catch (Exception e)
            {
                // should not happen in normal operation
                throw new Exception("", e);
            }
        }
    }
}