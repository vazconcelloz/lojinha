# Task 2 Report: UserService

## Summary

Implemented `UserService` (PBKDF2 password hashing via `Rfc2898DeriveBytes.Pbkdf2`, CRUD, and
`Authenticate` with constant-time hash comparison) exactly as specified in the task brief,
following RED → GREEN → full-suite → commit.

## Files changed

- `Lojinha.Services.Tests/UserServiceTests.cs` (new) — transcribed verbatim from brief.
- `Lojinha.Services/UserService.cs` (new) — transcribed verbatim from brief.

Both `Lojinha.Services` and `Lojinha.Services.Tests` projects, and their `ProjectReference`s,
already existed from prior tasks (Category/Product/Sales/Stock/Supplier services) — no `.csproj`
or `.sln` edits were needed.

## Deviations from brief

1. **Test count**: brief step 4 says "Expected: PASS, 10 tests total for this class" and step 5
   says "Expected: PASS, 52 tests total." The verbatim test code in the brief actually contains
   **11** `[Fact]` methods (counted: AnyUsers_ReturnsFalseWhenNoUsersExist,
   Add_CreatesUserWithHashedPassword, Add_ThrowsWhenUsernameAlreadyExists,
   Authenticate_ReturnsUserWithCorrectCredentials, Authenticate_ThrowsWithWrongPassword,
   Authenticate_ThrowsWithUnknownUsername, Update_WithoutNewPassword_KeepsOldPasswordWorking,
   Update_WithNewPassword_InvalidatesOldPassword, Delete_RemovesUserWhenNotLastAdmin,
   Delete_ThrowsWhenDeletingLastAdmin, Delete_RemovesVendedorFreely). Actual results: 11 passed
   for the class, 53 total for the full suite. This is a documentation miscount in the brief
   text, not a code discrepancy — the code was transcribed verbatim and both numbers are simply
   off by one from what's written. No action taken since the brief's *code* (which is what
   matters) was followed exactly.

No other deviations. Code matches the brief character-for-character (implementation and test
file).

## Test commands and output

### RED (Step 2)

```
dotnet test --filter "FullyQualifiedName~UserServiceTests"
```

Result: build failed as expected —

```
error CS0246: O nome do tipo ou do namespace "UserService" não pode ser encontrado
(está faltando uma diretiva using ou uma referência de assembly?)
[...\Lojinha.Services.Tests\Lojinha.Services.Tests.csproj]
```

### GREEN (Step 4)

```
dotnet test --filter "FullyQualifiedName~UserServiceTests"
```

Result:

```
Aprovado!  – Com falha:     0, Aprovado:    11, Ignorado:     0, Total:    11
```

### Full suite (Step 5)

```
dotnet test
```

Result:

```
Aprovado!  – Com falha:     0, Aprovado:    53, Ignorado:     0, Total:    53
```

(42 pre-existing + 11 new = 53, consistent with the off-by-one brief text noted above.)

## Commit

```
b453eae feat: add UserService (PBKDF2 password hashing, last-admin guard)
```

Only the two intended files (`Lojinha.Services/UserService.cs`,
`Lojinha.Services.Tests/UserServiceTests.cs`) were staged and committed — confirmed via
`git status` before `git add`.

## Self-review

- **PBKDF2 API call**: `Rfc2898DeriveBytes.Pbkdf2(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm, int outputLength) : byte[]`
  is a real static method added in .NET 8 (confirmed by successful compilation against the
  installed SDK — `dotnet --version` reports `8.0.422`). Parameters (SHA256, 100_000 iterations,
  16-byte salt via `RandomNumberGenerator.GetBytes(16)`, 32-byte output) match the brief and the
  test's byte-length assertions (`SenhaHash.Length == 32`, `SenhaSalt.Length == 16`).
- **Constant-time comparison**: `Authenticate` uses `CryptographicOperations.FixedTimeEquals(hashCalculado, user.SenhaHash)`
  exclusively — grepped the file to confirm no `==` or `SequenceEqual` is used to compare hash
  bytes anywhere.
- **Generic auth error**: both the "unknown username" and "wrong password" branches in
  `Authenticate` throw the identical message `"Usuário ou senha inválidos."`, so the exception
  never distinguishes which credential was wrong.
- **Last-admin guard**: `Delete` throws `InvalidOperationException` when the target user is
  `PapelUsuario.Admin` and the count of Admins is `<= 1`. Verified by tests
  `Delete_ThrowsWhenDeletingLastAdmin` (throws) and `Delete_RemovesUserWhenNotLastAdmin` /
  `Delete_RemovesVendedorFreely` (succeeds when not the last admin, or not an admin at all).
- **Update password rotation**: `Update_WithNewPassword_InvalidatesOldPassword` confirms the old
  password stops working and the new one works after `Update` with a non-null `novaSenha`;
  `Update_WithoutNewPassword_KeepsOldPasswordWorking` confirms passing `null` leaves the password
  hash/salt untouched.
- **Duplicate username guard**: both `Add` and `Update` check for existing usernames (`Update`
  correctly excludes the current row via `u.Id != id`) and throw `InvalidOperationException`.
- Diff review (`git status` pre-commit) confirmed no stray files were staged.

## Concerns

- **Timing side-channel (pre-existing in the brief, not introduced by me)**: `Authenticate`
  short-circuits with an immediate throw when the username is not found, but performs a full
  100,000-iteration PBKDF2 computation when the username exists but the password is wrong. This
  creates a timing difference between "unknown username" and "wrong password" that a
  sufhttp/timing attacker could in theory exploit to enumerate valid usernames, even though the
  *error message* is identical in both cases. This is exactly the code given verbatim in the
  brief, so I did not alter it, but flagging it here since the task description emphasizes this
  is security-sensitive code. A future hardening step (out of scope for this task) could perform
  a dummy PBKDF2 computation on the unknown-username path to equalize timing.
- Brief's stated test counts (10 / 52) are off by one from the verbatim code's actual test count
  (11 / 53) — noted above, no functional impact.
