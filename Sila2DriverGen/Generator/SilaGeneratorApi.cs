using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using Common.Logging;
using Tecan.Sila2.Generator.Contracts;
using Tecan.Sila2.Generator.Helper;

namespace Tecan.Sila2.Generator
{
    /// <summary>
    /// Public API for SiLA2 code generation that can be called directly from other projects
    /// </summary>
    public class SilaGeneratorApi : IDisposable
    {
        private readonly CompositionContainer _container;
        private readonly ILog _logger;
        private bool _disposed;

        /// <summary>
        /// Creates a new instance of the SiLA Generator API
        /// </summary>
        public SilaGeneratorApi()
        {
            try
            {
                _container = Program.CreateCompositionContainer();
                _logger = LogManager.GetLogger<SilaGeneratorApi>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize SiLA Generator API", ex);
            }
        }

        /// <summary>
        /// Generates interface code from a SiLA feature definition
        /// </summary>
        /// <param name="featurePath">Path to the SiLA feature XML file</param>
        /// <param name="outputPath">Path where the interface should be generated</param>
        /// <param name="ns">Namespace for the generated interface</param>
        public void GenerateInterface(string featurePath, string outputPath, string ns)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SilaGeneratorApi));
            
            try
            {
                _logger?.Info($"Loading feature from {featurePath}");
                var feature = FeatureSerializer.Load(Path.GetFullPath(featurePath));
                
                _logger?.Info("Generating interface code");
                var interfaceGenerator = _container.GetExportedValue<IInterfaceGenerator>();
                var unit = interfaceGenerator.GenerateInterfaceUnit(feature, ns ?? feature.Namespace);
                
                _logger?.Info($"Writing interface to {outputPath}");
                CodeGenerationHelper.GenerateCSharp(unit, outputPath);
                
                _logger?.Info("Interface generation completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to generate interface: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Generates provider (server and/or client) code from a SiLA feature definition
        /// </summary>
        /// <param name="featurePath">Path to the SiLA feature XML file</param>
        /// <param name="dtoPath">Path where the DTOs should be generated</param>
        /// <param name="providerPath">Path where the provider should be generated</param>
        /// <param name="clientPath">Path where the client should be generated (optional, for when both server and client are generated)</param>
        /// <param name="ns">Namespace for the generated code</param>
        /// <param name="clientOnly">If true, only generate client code</param>
        /// <param name="serverOnly">If true, only generate server code</param>
        /// <param name="importedNamespaces">Additional namespaces to import (semicolon-separated)</param>
        public void GenerateProvider(
            string featurePath, 
            string dtoPath, 
            string providerPath,
            string clientPath = null,
            string ns = null,
            bool clientOnly = false,
            bool serverOnly = false,
            string importedNamespaces = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SilaGeneratorApi));
            
            try
            {
                _logger?.Info($"Loading feature from {featurePath}");
                var feature = FeatureSerializer.Load(featurePath);
                var @namespace = ns ?? feature.Namespace;

                var dtoGenerator = _container.GetExportedValue<IDtoGenerator>();
                var providerGenerator = _container.GetExportedValue<IServerGenerator>();
                var clientGenerator = _container.GetExportedValue<IClientGenerator>();

                var generateClient = clientOnly || (!serverOnly && !clientOnly);
                var generateServer = serverOnly || (!serverOnly && !clientOnly);

                if (clientOnly)
                {
                    generateClient = true;
                    generateServer = false;
                }
                else if (serverOnly)
                {
                    generateClient = false;
                    generateServer = true;
                }

                _logger?.Info("Generating DTOs");
                var dtoUnit = dtoGenerator.GenerateInterfaceUnit(feature, @namespace);
                
                _logger?.Info("Generating provider code");
                var providerUnit = generateServer ? providerGenerator.GenerateServer(feature, @namespace) : null;
                
                _logger?.Info("Generating client code");
                var clientUnit = generateClient ? clientGenerator.GenerateClient(feature, @namespace) : null;

                var imports = string.IsNullOrEmpty(importedNamespaces) 
                    ? new string[0]
                    : importedNamespaces.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                _logger?.Debug("Adding additional import statements");
                AddUsingStatements(dtoUnit, imports);
                AddUsingStatements(providerUnit, imports);
                AddUsingStatements(clientUnit, imports);

                _logger?.Info("Generating code for data transfer objects");
                CodeGenerationHelper.GenerateCSharp(dtoUnit, Path.GetFullPath(dtoPath));

                if (generateServer)
                {
                    _logger?.Info("Generating code for provider");
                    CodeGenerationHelper.GenerateCSharp(providerUnit, Path.GetFullPath(providerPath));
                }

                if (generateClient)
                {
                    var actualClientPath = generateServer ? clientPath : providerPath;
                    if (string.IsNullOrEmpty(actualClientPath))
                    {
                        throw new ArgumentException("Client path must be specified when generating client code");
                    }
                    _logger?.Info($"Generating code for client to {actualClientPath}");
                    CodeGenerationHelper.GenerateCSharp(clientUnit, Path.GetFullPath(actualClientPath));
                }

                _logger?.Info("Provider generation completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to generate provider: {ex.Message}", ex);
                throw;
            }
        }

        private static void AddUsingStatements(System.CodeDom.CodeCompileUnit unit, string[] imports)
        {
            if (imports == null || imports.Length == 0 || unit == null) return;
            
            var ns = System.Linq.Enumerable.FirstOrDefault(
                System.Linq.Enumerable.Cast<System.CodeDom.CodeNamespace>(unit.Namespaces), 
                n => n.Name == null);
            
            if (ns == null)
            {
                ns = new System.CodeDom.CodeNamespace();
                unit.Namespaces.Add(ns);
            }

            foreach (var import in imports)
            {
                ns.Imports.Add(new System.CodeDom.CodeNamespaceImport(import));
            }
        }

        /// <summary>
        /// Disposes the MEF container
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _container?.Dispose();
                _disposed = true;
            }
        }
    }
}

