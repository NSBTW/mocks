using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FakeItEasy;
using FileSender.Dependencies;
using FluentAssertions;
using NUnit.Framework;

namespace FileSender
{
    public class FileSender
    {
        private readonly ICryptographer cryptographer;
        private readonly ISender sender;
        private readonly IRecognizer recognizer;

        public FileSender(
            ICryptographer cryptographer,
            ISender sender,
            IRecognizer recognizer)
        {
            this.cryptographer = cryptographer;
            this.sender = sender;
            this.recognizer = recognizer;
        }

        public Result SendFiles(File[] files, X509Certificate certificate)
        {
            return new Result
            {
                SkippedFiles = files
                    .Where(file => !TrySendFile(file, certificate))
                    .ToArray()
            };
        }

        private bool TrySendFile(File file, X509Certificate certificate)
        {
            Document document;
            if (!recognizer.TryRecognize(file, out document))
                return false;
            if (!CheckFormat(document) || !CheckActual(document))
                return false;
            var signedContent = cryptographer.Sign(document.Content, certificate);
            return sender.TrySend(signedContent);
        }

        private bool CheckFormat(Document document)
        {
            return document.Format == "4.0" ||
                   document.Format == "3.1";
        }

        private bool CheckActual(Document document)
        {
            return document.Created.AddMonths(1) > DateTime.Now;
        }

        public class Result
        {
            public File[] SkippedFiles { get; set; }
        }
    }

    //TODO: реализовать недостающие тесты
    [TestFixture]
    public class FileSender_Should
    {
        private FileSender fileSender;
        private ICryptographer cryptographer;
        private ISender sender;
        private IRecognizer recognizer;

        private readonly X509Certificate certificate = new X509Certificate();
        private File file;
        private byte[] signedContent;

        [SetUp]
        public void SetUp()
        {
            file = new File("someFile", new byte[] {1, 2, 3});
            signedContent = new byte[] {1, 7};

            cryptographer = A.Fake<ICryptographer>();
            sender = A.Fake<ISender>();
            recognizer = A.Fake<IRecognizer>();
            fileSender = new FileSender(cryptographer, sender, recognizer);
        }

        [TestCase("4.0")]
        [TestCase("3.1")]
        public void Send_WhenGoodFormat(string format)
        {
            var document = new Document(file.Name, file.Content, DateTime.Now, format);
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(true);

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Should().BeEmpty();
        }

        [TestCase("2.1")]
        [TestCase("1.1")]
        [TestCase("3.0")]
        public void Skip_WhenBadFormat(string format)
        {
            var document = new Document(file.Name, file.Content, DateTime.Now, format);
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(true);

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Length.Should().Be(1);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Skip_WhenOlderThanAMonth(int monthCount)
        {
            var time = DateTime.Now.AddMonths(-monthCount).AddDays(-1);
            var document = new Document(file.Name, file.Content, time, "3.1");
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(true);

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Length.Should().Be(1);
        }

        [TestCase(10)]
        [TestCase(15)]
        [TestCase(20)]
        [TestCase(28)]
        public void Send_WhenYoungerThanAMonth(int daysCount)
        {
            var time = DateTime.Now.AddDays(-daysCount);
            var document = new Document(file.Name, file.Content, time, "3.1");
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(true);

            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Length.Should().Be(0);
        }

        [Test]
        public void Skip_WhenSendFails()
        {
            var document = new Document(file.Name, file.Content, DateTime.Now, "3.1");
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(false);
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Length.Should().Be(1);
        }

        [Test]
        public void Skip_WhenNotRecognized()
        {
            var document = new Document(file.Name, file.Content, DateTime.Now, "3.1");
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(false);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .Returns(true);
            fileSender.SendFiles(new[] {file}, certificate)
                .SkippedFiles.Length.Should().Be(1);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeAreInvalid()
        {
            var file1 = new File("someFile1", new byte[] {1, 2, 3});
            var file2 = new File("someFile2", new byte[] {1, 2, 3});
            var file3 = new File("someFile3", new byte[] {1, 2, 3});
            var document1 = new Document(file1.Name, file.Content, DateTime.Now, "3.1");
            var document2 = new Document(file2.Name, file.Content, DateTime.Now, "1.1");
            var document3 = new Document(file3.Name, file.Content, DateTime.Now, "3.1");
            A.CallTo(() => recognizer.TryRecognize(file1, out document1)).Returns(true);
            A.CallTo(() => recognizer.TryRecognize(file2, out document2)).Returns(true);
            A.CallTo(() => recognizer.TryRecognize(file3, out document3)).Returns(true);
            A.CallTo(() => cryptographer.Sign(null, certificate)).WithAnyArguments()
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent)).WithAnyArguments()
                .Returns(true);
            fileSender.SendFiles(new[] {file1, file2, file3}, certificate)
                .SkippedFiles.Length.Should().Be(1);
        }

        [Test]
        public void IndependentlySend_WhenSeveralFilesAndSomeCouldNotSend()
        {
            var document = new Document(file.Name, file.Content, DateTime.Now, "3.1");
            A.CallTo(() => recognizer.TryRecognize(file, out document))
                .Returns(true);
            A.CallTo(() => cryptographer.Sign(document.Content, certificate))
                .Returns(signedContent);
            A.CallTo(() => sender.TrySend(signedContent))
                .ReturnsNextFromSequence(true, false, true, true);
            fileSender.SendFiles(new[] {file, file, file, file}, certificate)
                .SkippedFiles.Length.Should().Be(1);
        }
    }
}