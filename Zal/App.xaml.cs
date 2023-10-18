using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Firebase.Auth.Providers;
using Firebase.Auth.Repository;
using Firebase.Auth.UI;

namespace Zal
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
       public App()
        {
            FirebaseUI.Initialize(new FirebaseUIConfig
            {
                ApiKey = "AIzaSyDSj8N7DH3jtMOAa4hd7ytqMq2H_8iprmc",
                AuthDomain = "zal1-353509.firebaseapp.com",
                Providers = new FirebaseAuthProvider[]
               {
                    new GoogleProvider(),
                    
                    new EmailProvider()
               },
                PrivacyPolicyUrl = "https://github.com/step-up-labs/firebase-authentication-dotnet",
                TermsOfServiceUrl = "https://github.com/step-up-labs/firebase-database-dotnet",
                IsAnonymousAllowed = false,
                AutoUpgradeAnonymousUsers = true,
                UserRepository = new FileUserRepository("FirebaseSample"),
                // Func called when upgrade of anonymous user fails because the user already exists
                // You should grab any data created under your anonymous user, sign in with the pending credential
                // and copy the existing data to the new user
                // see details here: https://github.com/firebase/firebaseui-web#upgrading-anonymous-users
                AnonymousUpgradeConflict = conflict => conflict.SignInWithPendingCredentialAsync(true)
            });
        }
    }
}
