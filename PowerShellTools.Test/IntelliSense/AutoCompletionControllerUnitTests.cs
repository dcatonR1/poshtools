﻿using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using PowerShellTools.Intellisense;

namespace PowerShellTools.Test.IntelliSense
{
    [TestClass]
    public class AutoCompletionControllerUnitTests
    {
        private string _mockedScript;
        private int _mockedCaret;

        [TestMethod]
        public void ShouldCompleteBrace()
        {
            var autoCompletionController = FakeAutoCompletionController();
            autoCompletionController.SetAutoCompleteState(false);

            _mockedScript = @"MyFunc";
            _mockedCaret = 6;

            autoCompletionController.ProcessKeystroke((uint)VSConstants.VSStd2KCmdID.TYPECHAR, '(');
            Assert.AreEqual<string>(@"MyFunc()", _mockedScript);
            Assert.AreEqual<int>(7, _mockedCaret);
        }

        [TestMethod]
        public void ShouldCompleteQuotes()
        {
            var autoCompletionController = FakeAutoCompletionController();
            autoCompletionController.SetAutoCompleteState(false);

            _mockedScript = @"MyFunc";
            _mockedCaret = 6;

            autoCompletionController.ProcessKeystroke((uint)VSConstants.VSStd2KCmdID.TYPECHAR, '\"');
            Assert.AreEqual<string>(@"MyFunc""""", _mockedScript);
            Assert.AreEqual<int>(7, _mockedCaret);
        }

        [TestMethod]
        public void ShouldMoveToRightByTypingCompletedBrackets()
        {
            var autoCompletionController = FakeAutoCompletionController();
            autoCompletionController.SetAutoCompleteState(true);

            _mockedScript = @"MyFunc[]";
            _mockedCaret = 7;

            autoCompletionController.ProcessKeystroke((uint)VSConstants.VSStd2KCmdID.TYPECHAR, '\"');
            Assert.AreEqual<string>(_mockedScript, @"MyFunc[]");
            Assert.AreEqual<int>(_mockedCaret, 8);
        }

        [TestMethod]
        public void ShouldMoveToRightByTypingCompletedQuotes()
        {
            var autoCompletionController = FakeAutoCompletionController();
            autoCompletionController.SetAutoCompleteState(true);

            _mockedScript = @"MyFunc""""";
            _mockedCaret = 7;

            autoCompletionController.ProcessKeystroke((uint)VSConstants.VSStd2KCmdID.TYPECHAR, '\"');
            Assert.AreEqual<string>(_mockedScript, @"MyFunc""""");
            Assert.AreEqual<int>(_mockedCaret, 8);
        }

        [TestMethod]
        public void ShouldCompletedQuotesWhenNextCharIsNotSameQuotes()
        {
            var autoCompletionController = FakeAutoCompletionController();
            autoCompletionController.SetAutoCompleteState(true);

            _mockedScript = @"MyFunc()";
            _mockedCaret = 7;

            autoCompletionController.ProcessKeystroke((uint)VSConstants.VSStd2KCmdID.TYPECHAR, '\"');
            Assert.AreEqual<string>(_mockedScript, @"MyFunc("""")");
            Assert.AreEqual<int>(_mockedCaret, 8);
        }

        private void TestCompleteBraceOrQuotesHelper(string initialMockedScript, int initialMockedCaret, bool isLastCmdAutoComplete, uint nCmdID, IntPtr pvaIn)
        {
            var autoCompletionController = FakeAutoCompletionController();
            _mockedScript = initialMockedScript;
            _mockedCaret = initialMockedCaret;
            autoCompletionController.SetAutoCompleteState(isLastCmdAutoComplete);
            //autoCompletionController.ProcessKeystroke(nCmdID, pvaIn);
        }

        private AutoCompletionController FakeAutoCompletionController(string contentType = PowerShellConstants.LanguageName)
        {
            var textView = TextViewMock(contentType);
            var editorOperations = EditorOperationsMock();
            var textUndoHistory = TextUndoHistoryMock();
            Mock<SVsServiceProvider> serviceProvider = new Mock<SVsServiceProvider>();
            AutoCompletionController autoCompletionController = new AutoCompletionController(textView.Object,
                                                                                             editorOperations.Object,
                                                                                             textUndoHistory.Object,
                                                                                             serviceProvider.Object);

            return autoCompletionController;

        }

        private Mock<ITextView> TextViewMock(string contentType)
        {
            Mock<ITextView> textView = new Mock<ITextView>();
            textView.Setup(m => m.TextSnapshot.Length)
                    .Returns(() => _mockedScript.Length);
            textView.Setup(m => m.Caret)
                    .Returns(() => 
                                {
                                    var textSnapshot = new Mock<ITextSnapshot>();
                                    textSnapshot.Setup(m => m.Length)
                                                .Returns(_mockedScript.Length);
                                    var snapshotPoint = new SnapshotPoint(textSnapshot.Object, _mockedCaret);
                                    var virtualSnapshotPoint = new VirtualSnapshotPoint(snapshotPoint);
                                    var caretPosition = new CaretPosition(virtualSnapshotPoint, (new Mock<IMappingPoint>()).Object, PositionAffinity.Successor);

                                     Mock<ITextCaret> textCaret = new Mock<ITextCaret>();
                                     textCaret.SetupGet(m => m.Position)
                                              .Returns(caretPosition);
                                    return textCaret.Object;
                                });
            textView.Setup(m => m.TextBuffer.ContentType.TypeName)
                    .Returns(() => contentType);
            textView.Setup(m => m.TextSnapshot.CreateTrackingPoint(It.IsAny<int>(), It.IsAny<PointTrackingMode>()))
                    .Returns<int, PointTrackingMode>((p, q) =>
                                                            {
                                                                Mock<ITrackingPoint> trackingPoint = new Mock<ITrackingPoint>();
                                                                trackingPoint.Setup<char>(m => m.GetCharacter(It.IsAny<ITextSnapshot>()))
                                                                             .Returns(() => _mockedScript[q == PointTrackingMode.Positive ? p : p - 1]);
                                                                return trackingPoint.Object;
                                                            });

            return textView;
        }

        private Mock<IEditorOperations> EditorOperationsMock()
        {
            Mock<Microsoft.VisualStudio.Text.Operations.IEditorOperations> editorOperations = new Mock<IEditorOperations>();
            editorOperations.Setup(m => m.AddBeforeTextBufferChangePrimitive())
                            .Callback(() => { });
            editorOperations.Setup(m => m.AddAfterTextBufferChangePrimitive())
                            .Callback(() => { });

            // {|} --> {p|}
            editorOperations.Setup(m => m.InsertText(It.IsAny<string>()))
                            .Callback<string>(p =>
                                                {
                                                    _mockedScript = _mockedScript.Insert(_mockedCaret, p);
                                                    _mockedCaret++;
                                                });
            
            // {}| --> {|}  
            editorOperations.Setup(m => m.MoveToPreviousCharacter(false))
                            .Callback(() =>
                                        {
                                            _mockedCaret--;
                                        });

            // {|} --> {}|
            editorOperations.Setup(m => m.MoveToNextCharacter(false))
                            .Callback(() =>
                                        {
                                            _mockedCaret++;
                                        });
            
            // {|} --> {\r\n|}
            editorOperations.Setup(m => m.InsertNewLine())
                            .Callback(() =>
                                        {
                                            _mockedScript = _mockedScript.Insert(_mockedCaret, Environment.NewLine);
                                            _mockedCaret += Environment.NewLine.Length;
                                        });

            // {\r\n|} --> {|\r\n}
            editorOperations.Setup(m => m.MoveLineUp(false))
                            .Callback(() => 
                                        {
                                            _mockedCaret -= Environment.NewLine.Length;
                                        });

            editorOperations.Setup(m => m.MoveToEndOfLine(false))
                            .Callback(() =>
                                        {
                                            // Should move caret to the end, here we remain it still as it's already at the end.
                                        });

            // {\r\n|\r\n} --> {\r\n  |\r\n}
            editorOperations.Setup(m => m.Indent())
                            .Callback(() =>
                                        {
                                            const string fakedIndent = "  ";
                                            _mockedScript = _mockedScript.Insert(_mockedCaret, fakedIndent);
                                            _mockedCaret += fakedIndent.Length;
                                        });
            return editorOperations;
        }

        private Mock<ITextUndoHistory> TextUndoHistoryMock()
        {
            Mock<ITextUndoHistory> textUndoHistory = new Mock<ITextUndoHistory>();
            textUndoHistory.Setup(m => m.CreateTransaction(It.IsAny<string>()))
                           .Returns(() => 
                                        {
                                            Mock<ITextUndoTransaction> textUndoTransactioin = new Mock<ITextUndoTransaction>();
                                            textUndoTransactioin.Setup(m => m.Complete()).Callback(() => { });
                                            return textUndoTransactioin.Object;
                                        });
            textUndoHistory.Setup(m => m.Undo(It.IsAny<int>()))
                           .Callback<int>(p => { });
            return textUndoHistory;
        }
    }
}
