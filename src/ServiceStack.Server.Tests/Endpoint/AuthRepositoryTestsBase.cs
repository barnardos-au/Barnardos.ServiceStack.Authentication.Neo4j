using Funq;
using NUnit.Framework;
using ServiceStack.Auth;
using ServiceStack.Testing;

namespace ServiceStack.Server.Tests.Endpoint
{
    public abstract class AuthRepositoryTestsBase
    {
        private ServiceStackHost appHost;

        public abstract void ConfigureAuthRepo(Container container); 
        
        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            appHost = new BasicAppHost(typeof(AuthRepositoryTestsBase).Assembly) 
            {
                ConfigureAppHost = host =>
                {
                    host.Plugins.Add(new AuthFeature(() => new AuthUserSession(), new IAuthProvider[] {
                        new CredentialsAuthProvider(), 
                    })
                    {
                        IncludeRegistrationService = true,
                    });
                    
                    host.Plugins.Add(new SharpPagesFeature());
                },
                ConfigureContainer = container => {
                    ConfigureAuthRepo(container);
                    var authRepo = container.Resolve<IAuthRepository>();
                    
                    if (authRepo is IClearable clearable)
                    {
                        try { clearable.Clear(); } catch {}
                    }

                    authRepo.InitSchema();
                }
            }.Init();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => appHost.Dispose();

        [Test]
        public void Can_CreateUserAuth()
        {
            var authRepo = appHost.TryResolve<IAuthRepository>();
            
            var newUser = authRepo.CreateUserAuth(new AppUser
            {
                DisplayName = "Test User",
                Email = "user@gmail.com",
                FirstName = "Test",
                LastName = "User",
            }, "p@55wOrd");
            
            Assert.That(newUser.Email, Is.EqualTo("user@gmail.com"));

            var fromDb = authRepo.GetUserAuth((newUser as AppUser)?.Key ?? newUser.Id.ToString());
            Assert.That(fromDb.Email, Is.EqualTo("user@gmail.com"));

            newUser.FirstName = "Updated";
            authRepo.SaveUserAuth(newUser);
            
            var newSession = SessionFeature.CreateNewSession(null, "SESSION_ID");
            newSession.PopulateSession(newUser);

            var updatedUser = authRepo.GetUserAuth(newSession.UserAuthId);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser.FirstName, Is.EqualTo("Updated"));
        }

        [Test]
        public void Can_AddUserAuthDetails()
        {
            var authRepo = appHost.TryResolve<IAuthRepository>();
            
            var newUser = authRepo.CreateUserAuth(new AppUser
            {
                DisplayName = "Facebook User",
                Email = "user@fb.com",
                FirstName = "Face",
                LastName = "Book",
            }, "p@55wOrd");
            
            var newSession = SessionFeature.CreateNewSession(null, "SESSION_ID");
            newSession.PopulateSession(newUser);
            Assert.That(newSession.Email, Is.EqualTo("user@fb.com"));

            var fbAuthTokens = new AuthTokens
            {
                Provider = FacebookAuthProvider.Name,
                AccessTokenSecret = "AAADDDCCCoR848BAMkQIZCRIKnVWZAvcKWqo7Ibvec8ebV9vJrfZAz8qVupdu5EbjFzmMmbwUFDbcNDea9H6rOn5SVn8es7KYZD",
                UserId = "123456",
                DisplayName = "FB User",
                FirstName = "FB",
                LastName = "User",
                Email = "user@fb.com",
            };
            
            var userAuthDetails = authRepo.CreateOrMergeAuthSession(newSession, fbAuthTokens);
            Assert.That(userAuthDetails.Email, Is.EqualTo("user@fb.com"));

            var userAuthDetailsList = authRepo.GetUserAuthDetails(newSession.UserAuthId);
            Assert.That(userAuthDetailsList.Count, Is.EqualTo(1));
            Assert.That(userAuthDetailsList[0].Email, Is.EqualTo("user@fb.com"));
        }

    }
}