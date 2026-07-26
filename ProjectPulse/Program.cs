using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ProjectPulse.Application;
using ProjectPulse.Components;
using ProjectPulse.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddDbContextFactory<LeadDeskDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ILeadDeskService, LeadDeskService>();
builder.Services.Configure<AIWritingOptions>(builder.Configuration.GetSection("AI"));
builder.Services.AddScoped<IAIWritingService, AIWritingService>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
}).AddEntityFrameworkStores<LeadDeskDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
});
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LeadDeskDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.ExecuteSqlRawAsync("""
        IF COL_LENGTH('WorkItems', 'IsImportant') IS NULL
        BEGIN
            ALTER TABLE [WorkItems] ADD [IsImportant] bit NOT NULL CONSTRAINT [DF_WorkItems_IsImportant] DEFAULT 0;
            CREATE INDEX [IX_WorkItems_IsImportant_UpdatedAt] ON [WorkItems] ([IsImportant], [UpdatedAt]);
        END

        IF COL_LENGTH('ReleaseWorkItems', 'SortOrder') IS NULL
        BEGIN
            ALTER TABLE [ReleaseWorkItems] ADD [SortOrder] int NOT NULL CONSTRAINT [DF_ReleaseWorkItems_SortOrder] DEFAULT 0;
        END
        """);
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapPost("/api/ai-writing/improve", async (AIWritingRequestDto request, IAIWritingService writingService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Text)) return Results.BadRequest(new { message = "Please enter text first." });
    if (request.Text.Length > 4000) return Results.BadRequest(new { message = "Text is too long. Please keep it under 4000 characters." });

    var response = await writingService.ImproveTextAsync(request, cancellationToken);
    return Results.Ok(response);
});
app.MapPost("/auth/login", async (HttpContext http, SignInManager<ApplicationUser> signInManager) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var rememberMe = form.TryGetValue("remember", out var rememberValues) &&
        rememberValues.Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));
    var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);
    return Results.Redirect(result.Succeeded ? "/" : "/login?error=1");
}).DisableAntiforgery();
app.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
}).DisableAntiforgery();
app.Run();
