﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using CommandLine;
using Common.Logging;
using Microsoft.Build.Locator;
using Tecan.Sila2.Generator.Contracts;
using Tecan.Sila2.Generator.Generators;

namespace Tecan.Sila2.Generator
{
    /// <summary>
    /// The main class of the code generator
    /// </summary>
    public class Program
    {

        private static CompositionContainer _container;

        /// <summary>
        /// Creates and returns a new MEF composition container for code generation
        /// </summary>
        public static CompositionContainer CreateCompositionContainer()
        {
            var catalogs = new List<ComposablePartCatalog>();
            var directory = new DirectoryCatalog( AppDomain.CurrentDomain.BaseDirectory, "SilaGen.*.dll" );
            var silaGenExtensions = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ), "Tecan", "SilaGen" );
            catalogs.Add(directory);
            catalogs.Add(new AssemblyCatalog(typeof(Program).Assembly));
            if( Directory.Exists( silaGenExtensions ) )
            {
                catalogs.Add( new DirectoryCatalog( silaGenExtensions, "SilaGen.*.dll" ) );
            }
            
            var container = new CompositionContainer( new AggregateCatalog(catalogs) );

            if(container.GetExportedValueOrDefault<IDependencyInjectionGenerator>() == null)
            {
                container.ComposeExportedValue<IDependencyInjectionGenerator>( new MefGenerator() );
            }

            return container;
        }

        /// <summary>
        /// The entry point for SilaGen
        /// </summary>
        /// <param name="args">The commandline parameters</param>
        public static void Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            _container = CreateCompositionContainer();

            var verbTypes = _container.GetExportedValues<ICommandLineVerb>()
                .Select(verb => verb.GetType())
                .Distinct()
                .ToArray();

            Parser.Default.ParseArguments(args, verbTypes)
                .WithParsed<ICommandLineVerb>(verb =>
                {
                    try
                    {
                        verb.Execute( _container );
                    }
                    catch( Exception e )
                    {
                        LogManager.GetLogger<Program>().Error( "Code generation failed", e );
                        Environment.Exit( 1 );
                    }
                } );
        }
    }
}
