using System.Diagnostics;
using System.Reflection;
using mRemoteNG.Connection;
using mRemoteNG.Tools;
using NUnit.Framework;

namespace mRemoteNGTests.Tools
{
    [TestFixture]
    public class ExternalToolTests
    {
        [Test]
        public void PasswordWithEqualsSignIsPassedCorrectly()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Password = "Z-3=Wv99/Aq",
                Hostname = "testhost",
                Username = "testuser"
            };

            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "test.exe",
                Arguments = "-u %USERNAME% -p %PASSWORD% -h %HOSTNAME%"
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert
            // The arguments should contain the password with the equals sign
            // as a single, unescaped argument
            var arguments = string.Join(" ", process.StartInfo.ArgumentList);
            Assert.That(arguments, Does.Contain("Z-3=Wv99/Aq"));
        }

        [Test]
        public void PasswordWithSpecialCharactersIsPassedCorrectly()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Password = "P@ss=W0rd!",
                Hostname = "testhost",
                Username = "testuser"
            };

            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "test.exe",
                Arguments = "-p %PASSWORD%"
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert
            // The password should be present in the arguments as a single argument
            var arguments = string.Join(" ", process.StartInfo.ArgumentList);
            Assert.That(arguments, Does.Contain("P@ss=W0rd!"));
        }

        [Test]
        public void MultipleArgumentsAreParsedCorrectly()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Password = "TestPass=123",
                Hostname = "myhost.com",
                Username = "admin",
                Port = 8080
            };

            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "app.exe",
                Arguments = "--host %HOSTNAME% --port %PORT% --user %USERNAME% --pass %PASSWORD%"
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert
            var arguments = string.Join(" ", process.StartInfo.ArgumentList);
            Assert.That(arguments, Does.Contain("--host myhost.com"));
            Assert.That(arguments, Does.Contain("--port 8080"));
            Assert.That(arguments, Does.Contain("--user admin"));
            Assert.That(arguments, Does.Contain("--pass TestPass=123"));
        }
    }
}
