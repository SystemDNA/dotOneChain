using DynamicFormsRazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
builder.Services.AddSession(); // <-- Add this
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<FormDefinitionRepository>();
builder.Services.AddScoped<FormSubmissionRepository>();
builder.Services.AddScoped<SchemaValidator>();
builder.Services.AddHttpClient<BlockchainApiClient>();
var app = builder.Build();
app.UseSession();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
