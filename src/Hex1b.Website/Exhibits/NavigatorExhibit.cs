using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

#pragma warning disable HEX1B001 // Experimental Navigator API

namespace Hex1b.Website.Exhibits;

/// <summary>
/// A CRM demo using the fluent context-based API demonstrating stack-based navigation.
/// </summary>
public class NavigatorExhibit(ILogger<NavigatorExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<NavigatorExhibit> _logger = logger;

    public override string Id => "navigation";
    public override string Title => "Navigation";
    public override string Description => "A simple CRM system demonstrating stack-based navigation.";

    #region Domain Model

    private class Opportunity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "";
        public decimal Amount { get; set; }
    }

    private class Customer
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string CompanyName { get; set; } = "";
        public string Email { get; set; } = "";
        public List<Opportunity> Opportunities { get; } = [];
    }

    #endregion

    #region Application State

    /// <summary>
    /// All application state in one typed container.
    /// This is the TState for our Hex1bApp&lt;TState&gt;.
    /// </summary>
    private class CrmAppState
    {
        // Domain data
        public List<Customer> Customers { get; } = [];

        // Navigation state
        public NavigatorState Navigator { get; }

        // Form states
        public string CompanyNameInput { get; set; } = "";
        public string EmailInput { get; set; } = "";
        public string OpportunityNameInput { get; set; } = "";
        public string OpportunityAmountInput { get; set; } = "";

        // Selection tracking
        public string? SelectedCustomerId { get; set; }
        private readonly Dictionary<string, string?> _selectedOpportunityIds = new();

        public string? GetSelectedOpportunityId(string customerId) =>
            _selectedOpportunityIds.TryGetValue(customerId, out var id) ? id : null;
        
        public void SetSelectedOpportunityId(string customerId, string? opportunityId) =>
            _selectedOpportunityIds[customerId] = opportunityId;

        public CrmAppState()
        {
            // Initialize navigator with startup route
            Navigator = new NavigatorState(new NavigatorRoute("startup", BuildStartup));
        }

        #region Screen Builders using Fluent API

        /// <summary>
        /// Startup - routes to first-run or home based on data.
        /// </summary>
        private Hex1bWidget BuildStartup(NavigatorState nav)
        {
            var ctx = new RootContext();
            
            if (Customers.Count == 0)
            {
                return BuildFirstRun(ctx, nav);
            }
            return BuildHome(ctx, nav);
        }

        /// <summary>
        /// First-run experience using fluent API.
        /// </summary>
        private Hex1bWidget BuildFirstRun(RootContext ctx, NavigatorState nav)
        {
            return ctx.VStack(v => [
                v.Text("╭───────────────────────────────────────╮"),
                v.Text("│         Welcome to Mini CRM           │"),
                v.Text("╰───────────────────────────────────────╯"),
                v.Text(""),
                v.Text("This is your first time running the CRM."),
                v.Text(""),
                v.Text("To get started, you'll need to create"),
                v.Text("your first customer record."),
                v.Text(""),
                v.Button("Create First Customer →", _ => 
                    nav.Push("new-customer", n => BuildNewCustomer(ctx, n)))
            ]);
        }

        /// <summary>
        /// New customer form using fluent API.
        /// </summary>
        private Hex1bWidget BuildNewCustomer(RootContext ctx, NavigatorState nav)
        {
            return ctx.VStack(v => [
                v.Text("╭───────────────────────────────────────╮"),
                v.Text("│          New Customer                 │"),
                v.Text("╰───────────────────────────────────────╯"),
                v.Text(""),
                v.Text("Company Name:"),
                v.TextBox(CompanyNameInput, onTextChanged: args => CompanyNameInput = args.NewText),
                v.Text(""),
                v.Text("Email:"),
                v.TextBox(EmailInput, onTextChanged: args => EmailInput = args.NewText),
                v.Text(""),
                v.Button("Save Customer", _ => SaveNewCustomer(nav))
            ]).WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).Action(() => nav.Pop(), "Cancel");
            });
        }

        private void SaveNewCustomer(NavigatorState nav)
        {
            var customer = new Customer
            {
                CompanyName = CompanyNameInput,
                Email = EmailInput
            };
            Customers.Add(customer);

            // Clear form
            CompanyNameInput = "";
            EmailInput = "";

            // Navigate to home
            nav.Reset(new NavigatorRoute("home", BuildStartup));
        }

        /// <summary>
        /// Home screen - master/detail using fluent API.
        /// </summary>
        private Hex1bWidget BuildHome(RootContext ctx, NavigatorState nav)
        {
            // Build customer items list
            var customerItems = Customers
                .Select(c => c.CompanyName)
                .ToList();

            var selectedCustomer = SelectedCustomerId != null 
                ? Customers.FirstOrDefault(c => c.Id == SelectedCustomerId)
                : null;

            return ctx.Splitter(
                ctx.VStack(left => [
                    left.Text("Customers"),
                    left.Text("─────────────────"),
                    left.List(customerItems, e => SelectedCustomerId = e.SelectedIndex >= 0 && e.SelectedIndex < Customers.Count ? Customers[e.SelectedIndex].Id : null, null),
                    left.Text(""),
                    left.Button("+ New", _ => 
                        nav.Push("new-customer", n => BuildNewCustomer(ctx, n)))
                ]),
                BuildCustomerDetail(ctx, selectedCustomer, nav),
                leftWidth: 25
            );
        }

        /// <summary>
        /// Customer detail panel (right side of splitter).
        /// </summary>
        private Hex1bWidget BuildCustomerDetail(
            RootContext ctx,
            Customer? customer,
            NavigatorState nav)
        {
            if (customer == null)
            {
                return ctx.Layout(ctx.VStack(v => [
                    v.Text(""),
                    v.Text("  Select a customer")
                ]));
            }

            // Build opportunity items list
            var opportunityItems = customer.Opportunities
                .Select(o => $"{o.Name} - ${o.Amount:N0}")
                .ToList();

            var totalValue = customer.Opportunities.Sum(o => o.Amount);

            return ctx.Layout(ctx.VStack(v => [
                v.Text("Customer Details"),
                v.Text("────────────────────────"),
                v.Text(""),
                v.Text($"Company: {customer.CompanyName}"),
                v.Text($"Email:   {customer.Email}"),
                v.Text($"ID:      {customer.Id}"),
                v.Text(""),
                v.Text($"Opportunities ({customer.Opportunities.Count}) - Total: ${totalValue:N0}"),
                v.Text("────────────────────────"),
                v.List(opportunityItems, e => SetSelectedOpportunityId(customer.Id, e.SelectedIndex >= 0 && e.SelectedIndex < customer.Opportunities.Count ? customer.Opportunities[e.SelectedIndex].Id : null), null),
                v.HStack(h => [
                    h.Button("+ Add", _ => 
                        nav.Push("new-opportunity", n => BuildNewOpportunity(ctx, n, customer))),
                    h.Button("Delete", _ => 
                        DeleteSelectedOpportunity(customer))
                ])
            ]));
        }

        /// <summary>
        /// New opportunity form using fluent API.
        /// </summary>
        private Hex1bWidget BuildNewOpportunity(
            RootContext ctx,
            NavigatorState nav,
            Customer customer)
        {
            return ctx.VStack(v => [
                v.Text("╭───────────────────────────────────────╮"),
                v.Text($"│  New Opportunity for {customer.CompanyName,-16} │"),
                v.Text("╰───────────────────────────────────────╯"),
                v.Text(""),
                v.Text("Opportunity Name:"),
                v.TextBox(OpportunityNameInput, onTextChanged: args => OpportunityNameInput = args.NewText),
                v.Text(""),
                v.Text("Amount ($):"),
                v.TextBox(OpportunityAmountInput, onTextChanged: args => OpportunityAmountInput = args.NewText),
                v.Text(""),
                v.Button("Save Opportunity", _ => SaveNewOpportunity(nav, customer))
            ]).WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).Action(() => nav.Pop(), "Cancel");
            });
        }

        private void SaveNewOpportunity(NavigatorState nav, Customer customer)
        {
            if (decimal.TryParse(OpportunityAmountInput, out var amount))
            {
                var opportunity = new Opportunity
                {
                    Name = OpportunityNameInput,
                    Amount = amount
                };
                customer.Opportunities.Add(opportunity);
            }

            OpportunityNameInput = "";
            OpportunityAmountInput = "";
            nav.Pop();
        }

        private void DeleteSelectedOpportunity(Customer customer)
        {
            var selectedId = GetSelectedOpportunityId(customer.Id);
            if (selectedId == null) return;

            var opp = customer.Opportunities.FirstOrDefault(o => o.Id == selectedId);
            if (opp != null)
            {
                customer.Opportunities.Remove(opp);
                SetSelectedOpportunityId(customer.Id, null);
            }
        }

        #endregion
    }

    #endregion

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating widget builder for Navigator CRM demo");

        // Create fresh state for this session
        var state = new CrmAppState();

        return () =>
        {
            try
            {
                _logger.LogDebug("Building NavigatorWidget via fluent API, customer count: {Count}", 
                    state.Customers.Count);
                
                // Create root context and build the navigator widget with info bar
                var ctx = new RootContext();
                var navigator = ctx.Navigator(state.Navigator);
                
                // Build info bar with navigation hints
                var infoBar = ctx.InfoBar([
                    new InfoBarSection(" Mini CRM "),
                    new InfoBarSection(" | "),
                    new InfoBarSection($"Customers: {state.Customers.Count}"),
                    new InfoBarSection(" | "),
                    new InfoBarSection("Tab: Navigate  Enter: Select  Esc: Back")
                ]);
                
                return ctx.VStack(
                    v => [navigator.FillHeight(), infoBar]
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building NavigatorWidget");
                throw;
            }
        };
    }
}
