using LootLocker.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles LootLocker guest, white-label login, account creation, verification, and password recovery.
/// </summary>
public class AuthManager : MonoBehaviour
{
    private const int MinimumPasswordLength = 8;
    private const int MaximumFailedLoginAttempts = 5;
    private const int LoginLockoutDurationMinutes = 15;
    private const float LoginSecondsPerMinute = 60f;
    private const string FailedLoginAttemptsKeyPrefix = "LootLockerFailedLoginAttempts_";
    private const string LoginLockoutExpiryKeyPrefix = "LootLockerLoginLockoutExpiry_";
    private const string LoginLockoutMessage = "Too many failed attempts. Try again in ";
    private const string LoginLockoutMessageSuffix = " minute(s).";
    private const string GmailAddressErrorMessage = "Please use a valid Gmail address (example@gmail.com).";
    private const string LoginFailedMessage = "Wrong password, please try again.";
    private const string ServerUnavailableMessage = "Can't reach the server. Check your internet connection.";
    private const string SignUpGenericFailureMessage = "Sign up couldn't be completed. Please try again.";
    private const string LoginGenericFailureMessage = "Login couldn't be completed. Please try again.";
    private const string MainMenuSceneNameFallback = "MainMenu";
    private const string LocalGuestStartedMessage = "Offline guest started. Progress is local; scores aren't uploaded.";
    private const string AccountCreatedVerificationMessage = "Account created. Check your email and Spam folders. Use Resend Verification only if the message does not arrive.";
    private const string AccountCreatedVerifiedMessage = "Account created and verified. Logging in...";
    private const string PasswordResetSentMessage = "If an account exists for this email, a password-reset link has been requested. Check your inbox and Spam folders.";
    private const string PasswordResetRateLimitedMessage = "LootLocker is temporarily rate-limiting email requests. Wait before trying again.";
    private const string PasswordResetServerErrorMessage = "LootLocker couldn't process the reset request right now. Try again later.";
    private const string PasswordResetConfigurationErrorMessage = "LootLocker rejected the reset request. Check the White Label password-reset email settings and Console.";
    private const string PasswordResetFailedMessage = "Couldn't reach LootLocker. Check your connection and try again.";
    private const string AccountNeedsVerificationMessage = "LootLocker says this account isn't verified. Check your inbox and Spam, or request a verification email.";
    private const string VerificationResendPromptSuffix = " Select Resend Verification when ready.";
    private const string VerificationResendCooldownSuffix = " You can request another email in ";
    private const string VerificationResendSuccessMessage = "LootLocker accepted the verification-email request. Check your inbox and Spam folders.";
    private const string VerificationResendRateLimitedMessage = "LootLocker is limiting verification emails. Wait before retrying; check Spam or contact LootLocker support.";
    private const string VerificationResendFailedMessage = "LootLocker couldn't request a verification email. Check your connection and try again later.";
    private const string VerificationCooldownKey = "LootLockerExplicitVerificationResendCooldownUtcTicksV2";
    private const int VerificationResendCooldownSeconds = 900;
    private const float SecondsPerMinute = 60f;
    private const float VerificationButtonRefreshInterval = 1f;

    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signUpButton;
    [SerializeField] private Button guestButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button forgotPasswordButton;
    [SerializeField] private Button resendVerificationButton;
    [SerializeField] private string mainMenuSceneName = MainMenuSceneNameFallback;

    private bool requestInProgress;
    private float nextVerificationButtonRefreshTime;

    private void Update()
    {
        if (resendVerificationButton == null || requestInProgress ||
            Time.unscaledTime < nextVerificationButtonRefreshTime)
        {
            return;
        }

        nextVerificationButtonRefreshTime = Time.unscaledTime + VerificationButtonRefreshInterval;
        resendVerificationButton.interactable =
            Application.internetReachability != NetworkReachability.NotReachable &&
            GetVerificationCooldownSecondsRemaining() == 0;
    }

    /// <summary>
    /// Connects the generated login UI to this authentication manager.
    /// </summary>
    public void Configure(TMP_InputField emailField, TMP_InputField passwordField, TextMeshProUGUI messageText,
        Button login, Button signUp, Button guest, Button back, string destinationScene,
        Button forgotPassword = null, Button resendVerification = null)
    {
        emailInputField = emailField;
        passwordInputField = passwordField;
        statusText = messageText;
        loginButton = login;
        signUpButton = signUp;
        guestButton = guest;
        backButton = back;
        forgotPasswordButton = forgotPassword;
        resendVerificationButton = resendVerification;
        if (loginButton != null)
        {
            loginButton.interactable = !requestInProgress &&
                Application.internetReachability != NetworkReachability.NotReachable;
        }

        if (resendVerificationButton != null)
        {
            resendVerificationButton.interactable = !requestInProgress &&
                Application.internetReachability != NetworkReachability.NotReachable &&
                GetVerificationCooldownSecondsRemaining() == 0;
        }

        mainMenuSceneName = string.IsNullOrWhiteSpace(destinationScene)
            ? MainMenuSceneNameFallback
            : destinationScene;
    }

    /// <summary>
    /// Creates a LootLocker white-label account and requires email verification before gameplay login.
    /// </summary>
    public void OnSignUpClicked()
    {
        if (requestInProgress)
        {
            return;
        }

        string rawEmail = emailInputField != null ? emailInputField.text : string.Empty;
        string password = passwordInputField != null ? passwordInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(rawEmail) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Enter your email address and password.");
            return;
        }

        if (!TryNormalizeGmailAddress(rawEmail, out string email))
        {
            SetStatus(GmailAddressErrorMessage);
            return;
        }

        if (password.Length < MinimumPasswordLength)
        {
            SetStatus("Use a password with at least 8 characters.");
            return;
        }

        BeginRequest("Creating account...");
        LootLockerSDKManager.WhiteLabelSignUp(email, password, response =>
        {
            if (this == null)
            {
                return;
            }

            if (response == null || !response.success)
            {
                string errorCode = response != null && response.errorData != null
                    ? response.errorData.code
                    : string.Empty;
                string errorMessage = response != null && response.errorData != null
                    ? response.errorData.message
                    : string.Empty;
                string failureMessage = GetSignUpFailureMessage(
                    response != null ? response.statusCode : 0, errorCode, errorMessage);
                FinishRequest(failureMessage);
                return;
            }

            if (!string.IsNullOrWhiteSpace(response.VerifiedAt))
            {
                SetStatus(AccountCreatedVerifiedMessage);
                StartWhiteLabelLogin(email, password);
                return;
            }

            FinishRequest(AccountCreatedVerificationMessage);
        });
    }

    /// <summary>
    /// Logs in an existing white-label account and opens the main menu on success.
    /// </summary>
    public void OnLoginClicked()
    {
        if (requestInProgress)
        {
            return;
        }

        string rawEmail = emailInputField != null ? emailInputField.text : string.Empty;
        string password = passwordInputField != null ? passwordInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(rawEmail) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Enter your email address and password.");
            return;
        }

        if (!TryNormalizeGmailAddress(rawEmail, out string email))
        {
            SetStatus(GmailAddressErrorMessage);
            return;
        }

        int lockoutSecondsRemaining = GetLoginLockoutSecondsRemaining(email);
        if (lockoutSecondsRemaining > 0)
        {
            SetStatus(GetLoginLockoutMessage(lockoutSecondsRemaining));
            return;
        }

        BeginRequest("Logging in...");
        StartWhiteLabelLogin(email, password);
    }

    /// <summary>
    /// Requests a LootLocker password-reset email for the entered email address.
    /// </summary>
    public void OnForgotPasswordClicked()
    {
        if (requestInProgress)
        {
            return;
        }

        string rawEmail = emailInputField != null ? emailInputField.text : string.Empty;
        if (!TryNormalizeGmailAddress(rawEmail, out string email))
        {
            SetStatus(string.IsNullOrWhiteSpace(rawEmail)
                ? "Enter your email address first."
                : GmailAddressErrorMessage);
            return;
        }

        BeginRequest("Requesting password reset...");
        LootLockerSDKManager.WhiteLabelRequestPassword(email, response =>
        {
            if (this == null)
            {
                return;
            }

            if (response != null && response.success)
            {
                FinishRequest(PasswordResetSentMessage);
                return;
            }

            Debug.LogWarning(BuildEmailRequestDiagnostic("password reset", response));
            FinishRequest(GetEmailRequestFailureMessage(response));
        });
    }

    /// <summary>
    /// Explicitly requests another LootLocker verification email, subject to a local cooldown.
    /// </summary>
    public void OnResendVerificationClicked()
    {
        if (requestInProgress)
        {
            return;
        }

        string rawEmail = emailInputField != null ? emailInputField.text : string.Empty;
        if (!TryNormalizeGmailAddress(rawEmail, out string email))
        {
            SetStatus(string.IsNullOrWhiteSpace(rawEmail)
                ? "Enter your email address first."
                : GmailAddressErrorMessage);
            return;
        }

        int cooldownSecondsRemaining = GetVerificationCooldownSecondsRemaining();
        if (cooldownSecondsRemaining > 0)
        {
            int minutesRemaining = Mathf.CeilToInt(cooldownSecondsRemaining / SecondsPerMinute);
            SetStatus("Please wait " + minutesRemaining + " minute(s) before requesting another verification email.");
            return;
        }

        BeginRequest("Requesting verification email...");
        LootLockerSDKManager.WhiteLabelRequestVerification(email, response =>
        {
            if (this == null)
            {
                return;
            }

            if (IsVerificationRateLimited(response))
            {
                int retrySeconds = response.errorData != null && response.errorData.retry_after_seconds.HasValue
                    ? response.errorData.retry_after_seconds.Value
                    : VerificationResendCooldownSeconds;
                SaveVerificationCooldown(Mathf.Max(VerificationResendCooldownSeconds, retrySeconds));
                FinishRequest(VerificationResendRateLimitedMessage);
                return;
            }

            if (response != null && response.success)
            {
                SaveVerificationCooldown(VerificationResendCooldownSeconds);
                FinishRequest(VerificationResendSuccessMessage);
                return;
            }

            Debug.LogWarning(BuildEmailRequestDiagnostic("verification email", response));
            FinishRequest(VerificationResendFailedMessage);
        });
    }

    /// <summary>
    /// Starts a LootLocker guest session and opens the main menu on success.
    /// </summary>
    public void OnGuestClicked()
    {
        if (requestInProgress)
        {
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            StartLocalGuestSession();
            return;
        }

        BeginRequest("Starting guest session...");
        LootLockerSDKManager.StartGuestSession(response =>
        {
            if (this == null)
            {
                return;
            }

            if (response == null || !response.success)
            {
                LeaderboardManager.ActivateLocalGuestSession();
                FinishAndLoadMainMenu(LocalGuestStartedMessage);
                return;
            }

            LeaderboardManager.ClearLocalGuestSession();
            FinishAndLoadMainMenu("Guest session started.");
        });
    }
    private void StartLocalGuestSession()
    {
        LeaderboardManager.ActivateLocalGuestSession();
        FinishAndLoadMainMenu(LocalGuestStartedMessage);
    }



    private void StartWhiteLabelLogin(string email, string password)
    {
        LootLockerSDKManager.WhiteLabelLoginAndStartSession(email, password, true, response =>
        {
            if (this == null)
            {
                return;
            }

            LootLockerWhiteLabelLoginResponse loginResponse = response != null ? response.LoginResponse : null;
            if (loginResponse != null && !loginResponse.success)
            {
                if (IsAccountNotVerified(loginResponse))
                {
                    FinishRequest(GetLoginFailureMessage(loginResponse));
                    return;
                }

                if (IsInvalidCredentials(loginResponse.statusCode,
                    loginResponse.errorData != null ? loginResponse.errorData.code : string.Empty,
                    loginResponse.errorData != null ? loginResponse.errorData.message : string.Empty))
                {
                    FinishRequest(RecordFailedLoginAttempt(email));
                    return;
                }

                FinishRequest(GetLoginFailureMessage(loginResponse));
                return;
            }

            // Continue evaluating the LootLocker session result.
            if (response == null || !response.success)
            {
                FinishRequest(GetSessionFailureMessage(response != null ? response.SessionResponse : null));
                return;
            }

            // Successful authentication clears this email's local lockout history.


            ClearFailedLoginAttempts(email);
            LeaderboardManager.ClearLocalGuestSession();
            LeaderboardManager.EnsureWhiteLabelDisplayName(email,
                response.SessionResponse != null ? response.SessionResponse.player_ulid : string.Empty,
                response.SessionResponse != null ? response.SessionResponse.player_name : string.Empty);
            FinishAndLoadMainMenu("Login successful.");
        });
    }

    private int GetCurrentEmailLockoutSecondsRemaining()
    {
        string rawEmail = emailInputField != null ? emailInputField.text : string.Empty;
        return TryNormalizeGmailAddress(rawEmail, out string email)
            ? GetLoginLockoutSecondsRemaining(email)
            : 0;
    }

    private string RecordFailedLoginAttempt(string email)
    {
        string attemptsKey = GetFailedLoginAttemptsKey(email);
        int failedAttempts = PlayerPrefs.GetInt(attemptsKey, 0) + 1;
        PlayerPrefs.SetInt(attemptsKey, failedAttempts);

        if (failedAttempts >= MaximumFailedLoginAttempts)
        {
            int lockoutSeconds = LoginLockoutDurationMinutes * (int)LoginSecondsPerMinute;
            long expiryTicks = System.DateTime.UtcNow.AddSeconds(lockoutSeconds).Ticks;
            PlayerPrefs.SetString(GetLoginLockoutExpiryKey(email),
                expiryTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            return LoginFailedMessage + " " + GetLoginLockoutMessage(lockoutSeconds);
        }

        PlayerPrefs.Save();
        return LoginFailedMessage;
    }

    private static int GetLoginLockoutSecondsRemaining(string email)
    {
        string expiryKey = GetLoginLockoutExpiryKey(email);
        if (!PlayerPrefs.HasKey(expiryKey) ||
            !long.TryParse(PlayerPrefs.GetString(expiryKey),
                System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
                out long expiryTicks))
        {
            return 0;
        }

        if (expiryTicks < System.DateTime.MinValue.Ticks || expiryTicks > System.DateTime.MaxValue.Ticks)
        {
            ClearFailedLoginAttempts(email);
            return 0;
        }

        double secondsRemaining =
            (new System.DateTime(expiryTicks, System.DateTimeKind.Utc) - System.DateTime.UtcNow).TotalSeconds;
        if (secondsRemaining <= 0d)
        {
            ClearFailedLoginAttempts(email);
            return 0;
        }

        return Mathf.CeilToInt((float)secondsRemaining);
    }

    private static string GetLoginLockoutMessage(int secondsRemaining)
    {
        int minutesRemaining = Mathf.CeilToInt(secondsRemaining / LoginSecondsPerMinute);
        return LoginLockoutMessage + minutesRemaining + LoginLockoutMessageSuffix;
    }

    private static void ClearFailedLoginAttempts(string email)
    {
        PlayerPrefs.DeleteKey(GetFailedLoginAttemptsKey(email));
        PlayerPrefs.DeleteKey(GetLoginLockoutExpiryKey(email));
        PlayerPrefs.Save();
    }

    private static string GetFailedLoginAttemptsKey(string email)
    {
        return FailedLoginAttemptsKeyPrefix + GetEmailPreferenceHash(email);
    }

    private static string GetLoginLockoutExpiryKey(string email)
    {
        return LoginLockoutExpiryKeyPrefix + GetEmailPreferenceHash(email);
    }

    private static string GetEmailPreferenceHash(string email)
    {
        const uint FnvOffsetBasis = 2166136261u;
        const uint FnvPrime = 16777619u;
        uint hash = FnvOffsetBasis;
        string normalizedEmail = email != null ? email.ToLowerInvariant() : string.Empty;
        unchecked
        {
            for (int index = 0; index < normalizedEmail.Length; index++)
            {
                hash ^= normalizedEmail[index];
                hash *= FnvPrime;
            }
        }

        return hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetAccountNeedsVerificationMessage()
    {
        int cooldownSecondsRemaining = GetVerificationCooldownSecondsRemaining();
        if (cooldownSecondsRemaining <= 0)
        {
            return AccountNeedsVerificationMessage + VerificationResendPromptSuffix;
        }

        int minutesRemaining = Mathf.CeilToInt(cooldownSecondsRemaining / SecondsPerMinute);
        return AccountNeedsVerificationMessage + VerificationResendCooldownSuffix + minutesRemaining + " minute(s).";
    }

    private static int GetVerificationCooldownSecondsRemaining()
    {
        if (!PlayerPrefs.HasKey(VerificationCooldownKey) ||
            !long.TryParse(PlayerPrefs.GetString(VerificationCooldownKey),
                System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
                out long expiryTicks))
        {
            return 0;
        }

        if (expiryTicks < System.DateTime.MinValue.Ticks || expiryTicks > System.DateTime.MaxValue.Ticks)
        {
            PlayerPrefs.DeleteKey(VerificationCooldownKey);
            return 0;
        }

        System.DateTime expiry = new System.DateTime(expiryTicks, System.DateTimeKind.Utc);
        double secondsRemaining = (expiry - System.DateTime.UtcNow).TotalSeconds;
        if (secondsRemaining <= 0d)
        {
            PlayerPrefs.DeleteKey(VerificationCooldownKey);
            return 0;
        }

        return Mathf.CeilToInt((float)secondsRemaining);
    }

    private static void SaveVerificationCooldown(int cooldownSeconds)
    {
        int safeCooldownSeconds = Mathf.Max(1, cooldownSeconds);
        long expiryTicks = System.DateTime.UtcNow.AddSeconds(safeCooldownSeconds).Ticks;
        PlayerPrefs.SetString(VerificationCooldownKey,
            expiryTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private static string GetEmailRequestFailureMessage(LootLocker.LootLockerResponse response)
    {
        if (response == null || response.statusCode == 0)
        {
            return PasswordResetFailedMessage;
        }

        if (response.statusCode == 429)
        {
            return PasswordResetRateLimitedMessage;
        }

        if (response.statusCode >= 500)
        {
            return PasswordResetServerErrorMessage;
        }

        return PasswordResetConfigurationErrorMessage;
    }

    private static string BuildEmailRequestDiagnostic(string requestName, LootLocker.LootLockerResponse response)
    {
        if (response == null)
        {
            return "LootLocker " + requestName + " request failed without a response.";
        }

        string requestId = response.errorData != null ? response.errorData.request_id : string.Empty;
        string traceId = response.errorData != null ? response.errorData.trace_id : string.Empty;
        string errorCode = response.errorData != null ? response.errorData.code : string.Empty;
        return "LootLocker " + requestName + " request failed. HTTP " + response.statusCode +
               ", code " + (string.IsNullOrEmpty(errorCode) ? "unknown" : errorCode) +
               ", request ID " + (string.IsNullOrEmpty(requestId) ? "unknown" : requestId) +
               ", trace ID " + (string.IsNullOrEmpty(traceId) ? "unknown" : traceId) + ".";
    }

    private static bool IsVerificationRateLimited(LootLocker.LootLockerResponse response)
    {
        if (response == null)
        {
            return false;
        }

        string errorCode = response.errorData != null ? response.errorData.code : string.Empty;
        string errorMessage = response.errorData != null ? response.errorData.message : string.Empty;
        string responseText = response.text != null ? response.text : string.Empty;
        string normalizedCode = errorCode.ToLowerInvariant();
        string normalizedMessage = errorMessage.ToLowerInvariant();
        string normalizedText = responseText.ToLowerInvariant();
        return response.statusCode == 429 ||
               ContainsAny(normalizedCode, "rate_limit", "too_many_requests", "too_many_verification") ||
               ContainsAny(normalizedMessage, "too many verification emails", "too many requests", "rate limit") ||
               ContainsAny(normalizedText, "too many verification emails", "too many requests", "rate limit");
    }

    private static bool TryNormalizeGmailAddress(string email, out string normalizedEmail)
    {
        normalizedEmail = string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();

        const string gmailSuffix = "@gmail.com";
        if (!normalizedEmail.EndsWith(gmailSuffix, System.StringComparison.Ordinal))
        {
            return false;
        }

        int atIndex = normalizedEmail.IndexOf('@');
        if (atIndex <= 0 || atIndex != normalizedEmail.LastIndexOf('@') ||
            atIndex != normalizedEmail.Length - gmailSuffix.Length)
        {
            return false;
        }

        for (int i = 0; i < atIndex; i++)
        {
            char character = normalizedEmail[i];
            bool allowed = (character >= 'a' && character <= 'z') ||
                           (character >= '0' && character <= '9') ||
                           character == '.' || character == '_' || character == '+' || character == '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetSignUpFailureMessage(int statusCode, string errorCode, string errorMessage)
    {
        if (IsServerUnavailable(statusCode, errorCode, errorMessage))
        {
            return ServerUnavailableMessage;
        }

        if (IsEmailAlreadyRegistered(errorCode, errorMessage))
        {
            return "This email is already registered. Try logging in instead.";
        }

        return SignUpGenericFailureMessage;
    }

    private static string GetLoginFailureMessage(LootLockerWhiteLabelLoginResponse response)
    {
        if (response == null)
        {
            return LoginGenericFailureMessage;
        }

        if (IsAccountNotVerified(response))
        {
            return GetAccountNeedsVerificationMessage();
        }

        string errorCode = response.errorData != null ? response.errorData.code : string.Empty;
        string errorMessage = response.errorData != null ? response.errorData.message : string.Empty;
        if (IsServerUnavailable(response.statusCode, errorCode, errorMessage))
        {
            return ServerUnavailableMessage;
        }

        return IsInvalidCredentials(response.statusCode, errorCode, errorMessage)
            ? LoginFailedMessage
            : LoginGenericFailureMessage;
    }

    private static bool IsAccountNotVerified(LootLockerWhiteLabelLoginResponse response)
    {
        if (response == null)
        {
            return false;
        }

        string errorMessage = response.errorData != null ? response.errorData.message : string.Empty;
        string responseText = response.text != null ? response.text : string.Empty;
        string normalizedMessage = errorMessage.ToLowerInvariant();
        string normalizedText = responseText.ToLowerInvariant();
        return ContainsAny(normalizedMessage, "account not verified", "account is not verified", "not verified", "unverified") ||
               ContainsAny(normalizedText, "account not verified", "account is not verified", "not verified", "unverified");
    }

    private static string GetSessionFailureMessage(LootLockerSessionResponse response)
    {
        if (response == null)
        {
            return LoginGenericFailureMessage;
        }

        string errorCode = response.errorData != null ? response.errorData.code : string.Empty;
        string errorMessage = response.errorData != null ? response.errorData.message : string.Empty;
        return IsServerUnavailable(response.statusCode, errorCode, errorMessage)
            ? ServerUnavailableMessage
            : LoginGenericFailureMessage;
    }

    private static bool IsServerUnavailable(int statusCode, string errorCode, string errorMessage)
    {
        string normalizedCode = errorCode != null ? errorCode.Trim() : string.Empty;
        if (statusCode == 0 && normalizedCode.Equals("HTTP0", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string message = errorMessage != null ? errorMessage.ToLowerInvariant() : string.Empty;
        return message.Contains("check your internet connection") ||
               message.Contains("network error") ||
               message.Contains("connection error") ||
               message.Contains("could not connect") ||
               message.Contains("unable to connect") ||
               message.Contains("failed to connect") ||
               message.Contains("could not be reached");
    }

    private static bool IsEmailAlreadyRegistered(string errorCode, string errorMessage)
    {
        string code = errorCode != null ? errorCode.ToLowerInvariant() : string.Empty;
        string message = errorMessage != null ? errorMessage.ToLowerInvariant() : string.Empty;
        return ContainsAny(code, "email_already_exists", "email_already_registered", "email_exists", "email_taken",
                   "user_already_exists", "user_exists") ||
               ContainsAny(message, "already registered", "already exists", "already taken", "email already",
                   "email has been taken", "email is taken", "duplicate email", "account with this email already exists");
    }

    private static bool IsInvalidCredentials(int statusCode, string errorCode, string errorMessage)
    {
        if (statusCode == 401)
        {
            return true;
        }

        string code = errorCode != null ? errorCode.ToLowerInvariant() : string.Empty;
        string message = errorMessage != null ? errorMessage.ToLowerInvariant() : string.Empty;
        return ContainsAny(code, "invalid_credentials", "invalid_email_or_password", "incorrect_credentials") ||
               ContainsAny(message, "invalid credentials", "invalid login credentials", "invalid email or password",
                   "incorrect email or password", "email or password is incorrect", "wrong email or password",
                   "email not found", "user not found", "player does not exist", "incorrect password", "wrong password");
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.Contains(terms[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void BeginRequest(string message)
    {
        requestInProgress = true;
        SetButtonsInteractable(false);
        SetStatus(message);
    }

    private void FinishRequest(string message)
    {
        requestInProgress = false;
        SetButtonsInteractable(true);
        SetStatus(message);
    }

    private void FinishAndLoadMainMenu(string message)
    {
        requestInProgress = false;
        SetButtonsInteractable(true);
        SetStatus(message);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loginButton != null)
        {
            loginButton.interactable = interactable;
        }

        if (signUpButton != null)
        {
            signUpButton.interactable = interactable;
        }

        if (guestButton != null)
        {
            guestButton.interactable = interactable;
        }

        if (backButton != null)
        {
            backButton.interactable = interactable;
        }

        if (forgotPasswordButton != null)
        {
            forgotPasswordButton.interactable = interactable;
        }

        if (resendVerificationButton != null)
        {
            resendVerificationButton.interactable = interactable && GetVerificationCooldownSecondsRemaining() == 0;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
