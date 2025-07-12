using AccountService.Infrastructure;
using AccountService.Infrastructure.Projections;
using AccountService.Infrastructure.Security;
using AccountService.Presentation;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString(
        "mongodb://root:1234@localhost:27018/?retryWrites=true");
    return new MongoClient(settings);
});

builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var db = client.GetDatabase("CashFlowDb");
    return db.GetCollection<UserProjection>("user");
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapPost("/register", async (UserDto userDto, IMongoCollection<UserProjection> db) =>
{
    var passwordHasher = new PasswordHasher<UserHash>();

    var userHash = new UserHash();

    var passwordHash = passwordHasher.HashPassword(userHash, userDto.Password);

    var exists = await db.Find(f => f.Email == userDto.Email).FirstOrDefaultAsync();
    if (exists != null)
        return Results.BadRequest("User already exists");

    var accountId = Guid.NewGuid().ToString();

    var user = new UserProjection
    {
        Name = userDto.Username,
        Email = userDto.Email,
        PasswordHash = passwordHash,
        AccountId = accountId
    };

    await db.InsertOneAsync(user);

    // Create JWT token
    var tokenString = JwtBearerAuthenticationOptions.Authenticate(accountId, user.Name, user.Email);

    return Results.Ok(new UserResultDto(accountId, tokenString));
});

app.MapPost("/login", async (LoginDto login, IMongoCollection<UserProjection> db) =>
{
    var user = await db.Find(f => f.Email == login.Email).FirstOrDefaultAsync();

    if (user == null)
        return Results.Unauthorized();

    var passwordHasher = new PasswordHasher<LoginDto>();
    var result = passwordHasher.VerifyHashedPassword(login, user.PasswordHash, login.Password);

    if (result == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    // Create JWT token
    var tokenString = JwtBearerAuthenticationOptions.Authenticate(user.AccountId, user.Name, user.Email);

    return Results.Ok(new { access_token = tokenString });
});


app.Run();