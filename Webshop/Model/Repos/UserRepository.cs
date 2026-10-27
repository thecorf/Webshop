using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Isopoh.Cryptography.Argon2;
using Webshop.Model;

namespace Webshop.Model.Repos;

public sealed class UserRepository : SqlRepositoryBase, IUserRepository
{
    private const string TableName = "Users";
    private const string IdColumn = "Id";
    private const string UserNameColumn = "UserName";
    private const string FullNameColumn = "FullName";
    private const string EmailColumn = "Email";
    private const string PasswordHashColumn = "PasswordHash";
    private const string RoleColumn = "Role";

    public UserRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public async Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default)
    {
        var sql = $"SELECT {IdColumn}, {UserNameColumn}, {FullNameColumn}, {EmailColumn}, {PasswordHashColumn}, {RoleColumn} FROM {TableName} WHERE {IdColumn} = @id;";

        var parameters = new[]
        {
            new SqlParameter("@id", SqlDbType.Int) { Value = id }
        };

        var rows = await QueryAsync(sql, parameters, MapUser, ct).ConfigureAwait(false);
        foreach (var u in rows) return u;
        return null;
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var sql = $"SELECT {IdColumn}, {UserNameColumn}, {FullNameColumn}, {EmailColumn}, {PasswordHashColumn}, {RoleColumn} FROM {TableName} WHERE {UserNameColumn} = @username;";
        var parameters = new[]
        {
            new SqlParameter("@username", SqlDbType.NVarChar, 200) { Value = username ?? string.Empty }
        };

        var rows = await QueryAsync(sql, parameters, MapUser, ct).ConfigureAwait(false);
        foreach (var u in rows) return u;
        return null;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var sql = $"SELECT {IdColumn}, {UserNameColumn}, {FullNameColumn}, {EmailColumn}, {PasswordHashColumn}, {RoleColumn} FROM {TableName} WHERE {EmailColumn} = @email;";
        var parameters = new[]
        {
            new SqlParameter("@email", SqlDbType.NVarChar, 320) { Value = email ?? string.Empty }
        };

        var rows = await QueryAsync(sql, parameters, MapUser, ct).ConfigureAwait(false);
        foreach (var u in rows) return u;
        return null;
    }

    public async Task<User> CreateUserAsync(User user, string password, CancellationToken ct = default)
    {
        if (user is null) throw new ArgumentNullException(nameof(user));
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password must be provided.", nameof(password));

        var hashed = Argon2.Hash(password);

        return await InTransactionAsync<User>(async (conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $@"INSERT INTO {TableName} ({UserNameColumn}, {FullNameColumn}, {EmailColumn}, {PasswordHashColumn}, {RoleColumn})
VALUES (@username, @fullname, @email, @passwordHash, @role);
SELECT CAST(SCOPE_IDENTITY() AS int);";
            cmd.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 200) { Value = user.UserName });
            cmd.Parameters.Add(new SqlParameter("@fullname", SqlDbType.NVarChar, 200) { Value = user.FullName });
            cmd.Parameters.Add(new SqlParameter("@email", SqlDbType.NVarChar, 320) { Value = user.Email });
            cmd.Parameters.Add(new SqlParameter("@passwordHash", SqlDbType.NVarChar, -1) { Value = hashed });
            cmd.Parameters.Add(new SqlParameter("@role", SqlDbType.Int) { Value = (int)user.Role });

            var idObj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            var id = Convert.ToInt32(idObj);

            return new User { Id = id, UserName = user.UserName, FullName = user.FullName, Email = user.Email, PasswordHash = hashed, Role = user.Role };
        });
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newPassword)) throw new ArgumentException("newPassword must be provided", nameof(newPassword));
        var hash = Argon2.Hash(newPassword);

        var sql = $"UPDATE {TableName} SET {PasswordHashColumn} = @hash WHERE {IdColumn} = @id;";
        var parameters = new[]
        {
            new SqlParameter("@hash", SqlDbType.NVarChar, -1) { Value = hash },
            new SqlParameter("@id", SqlDbType.Int) { Value = userId }
        };

        var affected = await ExecuteAsync(sql, parameters, ct).ConfigureAwait(false);
        return affected == 1;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        if (user is null) throw new ArgumentNullException(nameof(user));

        var sql = $"UPDATE {TableName} SET {UserNameColumn} = @username, {FullNameColumn} = @fullname, {EmailColumn} = @email, {RoleColumn} = @role WHERE {IdColumn} = @id;";
        var parameters = new[]
        {
            new SqlParameter("@username", SqlDbType.NVarChar, 200) { Value = user.UserName },
            new SqlParameter("@fullname", SqlDbType.NVarChar, 200) { Value = user.FullName },
            new SqlParameter("@email", SqlDbType.NVarChar, 320) { Value = user.Email },
            new SqlParameter("@role", SqlDbType.Int) { Value = (int)user.Role },
            new SqlParameter("@id", SqlDbType.Int) { Value = user.Id }
        };

        await ExecuteAsync(sql, parameters, ct).ConfigureAwait(false);
    }

    private static User MapUser(SqlDataReader r)
    {
        var roleOrd = r.GetOrdinal(RoleColumn);
        return new User
        {
            Id = r.GetInt32(r.GetOrdinal(IdColumn)),
            UserName = r.GetString(r.GetOrdinal(UserNameColumn)),
            FullName = r.GetString(r.GetOrdinal(FullNameColumn)),
            Email = r.GetString(r.GetOrdinal(EmailColumn)),
            PasswordHash = r.GetString(r.GetOrdinal(PasswordHashColumn)),
            Role = (User.UserRole)r.GetInt32(roleOrd)
        };
    }
}
