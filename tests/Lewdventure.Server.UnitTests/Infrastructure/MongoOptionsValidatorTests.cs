using Server.Infrastructure.Mongo;

namespace Tests.Unit.Infrastructure
{
    [TestFixture]
    public sealed class MongoOptionsValidatorTests
    {
        [Test]
        public void Validate_Disabled_Succeeds()
        {
            var result = new MongoOptionsValidator().Validate(null, new MongoOptions());

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_EnabledWithValidSettings_Succeeds()
        {
            var options = new MongoOptions
            {
                Enabled = true,
                ConnectionString = "mongodb://app:secret@mongo:27017/?replicaSet=rs0&authSource=lewdventure_dev",
                DatabaseName = "lewdventure_dev",
            };

            var result = new MongoOptionsValidator().Validate(null, options);

            Assert.That(result.Succeeded, Is.True, result.FailureMessage);
        }

        [TestCase("")]
        [TestCase("not a connection string")]
        public void Validate_InvalidConnectionString_Fails(string connectionString)
        {
            var options = new MongoOptions { Enabled = true, ConnectionString = connectionString, DatabaseName = "lewdventure_dev" };

            var result = new MongoOptionsValidator().Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }

        [TestCase("")]
        [TestCase("Lewdventure")]
        [TestCase("lewdventure-dev")]
        [TestCase("lewdventure.dev")]
        public void Validate_InvalidDatabaseName_Fails(string databaseName)
        {
            var options = new MongoOptions { Enabled = true, ConnectionString = "mongodb://localhost:27017", DatabaseName = databaseName };

            var result = new MongoOptionsValidator().Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }
    }
}
