﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Reductech.EDR.ConnectorManagement;
using Reductech.EDR.Core.Internal;

namespace LanguageServer
{
    internal class DocumentManager
    {
        private readonly ConcurrentDictionary<string, SCLDocument> _documents = new();

        private readonly ILogger<DocumentManager> _logger;

        private readonly ILanguageServerFacade _facade;

        private readonly AsyncLazy<StepFactoryStore> _stepFactoryStore;
        

        public DocumentManager(ILanguageServerFacade facade, ILogger<DocumentManager> logger, IConnectorManager connectorManager)
        {
            _facade = facade;
            _logger = logger;
            _stepFactoryStore =
                new AsyncLazy<StepFactoryStore>(() =>
                    connectorManager.GetStepFactoryStoreAsync(CancellationToken.None));
        }

        public void RemoveDocument(DocumentUri documentUri)
        {
            _documents.Remove(documentUri.ToString(), out var _);
        }

        public async Task UpdateDocument( SCLDocument document)
        {
            _documents.AddOrUpdate(document.DocumentUri.ToString(), document, (_, _) => document);

            var sfs = await _stepFactoryStore.Value;


            var diagnostics =document.GetDiagnostics(sfs);

            var diagnosticCount = diagnostics.Diagnostics?.Count() ?? 0;

            _logger.LogDebug($"Publishing {diagnosticCount} diagnostics for {document.DocumentUri}");

            _facade.TextDocument.PublishDiagnostics(diagnostics);


        }

        public SCLDocument? GetDocument(DocumentUri documentPath)
        {
            return _documents.TryGetValue(documentPath.ToString(), out var document) ? document : null;
        }
    }
}