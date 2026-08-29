# Allen and Kerber Auto Supply

The application uses Google Application Default Credentials (ADC) for Firestore,
Cloud Storage, and Secret Manager in every environment. No service-account key is
stored in this project.

## Development setup

1. Install the Google Cloud CLI and authenticate ADC:

   ```powershell
   gcloud auth application-default login
   gcloud auth application-default set-quota-project YOUR_PROJECT_ID
   ```

2. Ensure the authenticated account can access the configured Google Cloud
   project, Firestore database, and image bucket.

3. Set Google credentials in local configuration or environment variables:

   ```powershell
   $env:ExternalAuth__Google__ClientId="YOUR_GOOGLE_CLIENT_ID"
   $env:ExternalAuth__Google__ClientSecret="YOUR_GOOGLE_CLIENT_SECRET"
   ```

   The app automatically skips Google sign-in if either value is missing, so it will still start without live Google credentials during local development.

4. Configure these exact Google OAuth redirect URIs in the Google Cloud OAuth client:

   - Production: `https://www.allenandkerberautosupply.net/auth/google-callback`
   - Local: `https://localhost:4595/auth/google-callback`

5. Start the application:

   ```powershell
   dotnet run --project .\AllenKerberAutoSupply.csproj --launch-profile AllenKerberAutoSupply
   ```

The Google libraries automatically discover ADC. For a non-user local
credential, set `GOOGLE_APPLICATION_CREDENTIALS` to a credential file path
without committing that file.

## User provisioning and authorization

Both Google and email/password sign-in require a matching document in the
Firestore `users` collection. The document is keyed by the normalized
lowercase email (the store also supports an `Email` field lookup) and uses
these fields:

- `Email`: the user's email address
- `DisplayName`: the name shown after sign-in
- `PasswordHash`: an ASP.NET Identity password hash for email/password users;
  it may be empty for Google-only users
- `MustChangePassword`: when `true`, the account must replace its temporary
  password after its next email/password sign-in
- `Roles`: one or more of `InvoiceAdmin`, `InvoiceUser`,
  `CustomerInvoiceUser`, `SalesAdmin`, or `SalesUser`

There is no public registration or access-request workflow. User documents and
password hashes can be provisioned by a user with an `InvoiceAdmin` or
`SalesAdmin` role through the **User administration** workspace. New users and
password resets receive a temporary password and must change it before accessing
protected application features. A Google
account is not granted access unless its verified email matches a provisioned
document with a recognized role.
