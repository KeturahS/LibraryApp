using Hangfire;
using LibraryApp.Controllers;
using LibraryApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache(); // Required for session state
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout of half an hour
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("myConnect"));
});
builder.Services.AddHangfireServer();





var app = builder.Build();
app.UseSession();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();


app.UseHangfireDashboard("/hangfire");

RecurringJob.AddOrUpdate<IAdminService>(
    "daily-borrowing-reminder",
    service => service.SendBorrowingReminders(),
    Cron.Daily);




app.MapControllerRoute(
	name: "default",
	pattern: "{controller=HomePage}/{action=HomePage}/{id?}");



app.Run();



