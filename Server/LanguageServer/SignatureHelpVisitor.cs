﻿using System.Linq;
using System.Reflection;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Namotion.Reflection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Reductech.EDR.Core.Internal;
using Reductech.EDR.Core.Internal.Parser;

namespace LanguageServer
{
    public class SignatureHelpVisitor : SCLBaseVisitor<SignatureHelp?>
    {
        /// <inheritdoc />
        public SignatureHelpVisitor(Position position, StepFactoryStore stepFactoryStore)
        {
            Position = position;
            StepFactoryStore = stepFactoryStore;
        }

        public Position Position { get; }
        public StepFactoryStore StepFactoryStore { get; }

        /// <inheritdoc />
        public override SignatureHelp? Visit(IParseTree tree)
        {
            if (tree is ParserRuleContext context)
            {
                if (context.ContainsPosition(Position))
                {
                    return base.Visit(tree);
                }
                else if (context.EndsBefore(Position) && !context.HasSiblingsAfter(Position))
                {
                    //This position is at the end of this line - enter anyway
                    return base.Visit(tree);
                }
            }

            return DefaultResult;
        }


        /// <inheritdoc />
        public override SignatureHelp? VisitFunction(SCLParser.FunctionContext context)
        {
            var name = context.NAME().GetText();

            if (!context.ContainsPosition(Position))
            {
                if (context.EndsBefore(Position) &&
                    context.Stop.IsSameLineAs(Position)) //This position is on the line after the step definition
                {
                    if (!StepFactoryStore.Dictionary.TryGetValue(name, out var stepFactory))
                        return null; //No clue what name to use

                    return StepParametersSignatureHelp(stepFactory, new Range(Position, Position));
                }

                return null;
            }


            if (context.NAME().Symbol.ContainsPosition(Position))
            {
                return null;
            }

            var positionalTerms = context.term();

            for (var index = 0; index < positionalTerms.Length; index++)
            {
                var term = positionalTerms[index];

                if (term.ContainsPosition(Position))
                {
                    return Visit(term);
                }
            }

            foreach (var namedArgumentContext in context.namedArgument())
            {
                if (namedArgumentContext.ContainsPosition(Position))
                {
                    if (namedArgumentContext.NAME().Symbol.ContainsPosition(Position))
                    {
                        if (!StepFactoryStore.Dictionary.TryGetValue(name, out var stepFactory))
                            return null; //Don't know what step factory to use

                        var range = namedArgumentContext.NAME().Symbol.GetRange();

                        return StepParametersSignatureHelp(stepFactory, range);
                    }


                    return Visit(namedArgumentContext);
                }
            }

            {
                if (!StepFactoryStore.Dictionary.TryGetValue(name, out var stepFactory))
                    return null; //No clue what name to use


                return StepParametersSignatureHelp(stepFactory, new Range(Position, Position));
            }
        }


        public static SignatureHelp StepParametersSignatureHelp(IStepFactory stepFactory, Range range)
        {
            var documentation = Helpers.GetMarkDownDocumentation(stepFactory);
            var options =
                stepFactory.ParameterDictionary
                    .Where(x => x.Key is StepParameterReference.Named)
                    .Select(x => CreateCompletionItem(x.Key, x.Value))
                    .ToList();


            ParameterInformation CreateCompletionItem(StepParameterReference stepParameterReference,
                PropertyInfo propertyInfo)
            {
                return new()
                {
                    Label = stepParameterReference.Name,
                    Documentation = new StringOrMarkupContent(new MarkupContent()
                    {
                        Kind = MarkupKind.Markdown,
                        Value = documentation
                    })
                };
            }

            return new SignatureHelp()
            {
                Signatures = new Container<SignatureInformation>(new SignatureInformation
                {
                    Label = stepFactory.TypeName,
                    Documentation = stepFactory.StepType.GetXmlDocsSummary(),
                    Parameters = new Container<ParameterInformation>(options)
                })
            };
        }

    }
}