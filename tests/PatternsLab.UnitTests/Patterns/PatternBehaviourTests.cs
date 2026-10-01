using PatternsLab.Patterns.Behavioural;
using PatternsLab.Patterns.Creational;
using PatternsLab.Patterns.Structural;
using Shouldly;

namespace PatternsLab.UnitTests.Patterns;

/// <summary>
/// The demos print a transcript, but the patterns underneath them are ordinary code and
/// deserve ordinary tests. These also double as worked answers for the exercises.
/// </summary>
public class PatternBehaviourTests
{
    [Fact]
    public void FactoryMethod_returns_a_different_implementation_per_channel()
    {
        var factory = new NotifierFactory();

        factory.Create("email").ShouldBeOfType<EmailNotifier>();
        factory.Create("sms").ShouldBeOfType<SmsNotifier>();
        factory.Create("push").ShouldBeOfType<PushNotifier>();
        Should.Throw<ArgumentOutOfRangeException>(() => factory.Create("telegram"));
    }

    [Fact]
    public void Builder_refuses_to_produce_a_half_built_object()
    {
        Should.Throw<InvalidOperationException>(() => new ClaimBuilder().Build());

        var claim = new ClaimBuilder()
            .ForPatient(Guid.CreateVersion7())
            .BilledTo("PBM-01")
            .AddLine("A4253", 100, 0.42m)
            .Build();

        claim.Total.ShouldBe(42.00m);
    }

    [Fact]
    public void Adapter_turns_a_vendor_exception_into_our_own_result_shape()
    {
        var gateway = new LegacyPayGatewayAdapter(new LegacyPayXmlClient());

        gateway.Charge(42.50m, "USD", "CLM-001").Succeeded.ShouldBeTrue();
        gateway.Charge(25_000m, "USD", "CLM-002").FailureReason.ShouldBe("LIMIT_EXCEEDED");
    }

    [Fact]
    public void Decorator_chain_absorbs_the_second_read()
    {
        var real = new DatabaseClaimReader();
        var reader = new LoggingClaimReader(new CachingClaimReader(real));

        var id = Guid.CreateVersion7();
        reader.Read(id);
        reader.Read(id);

        real.Reads.ShouldBe(1);
    }

    [Fact]
    public void Composite_totals_a_whole_tree_without_the_caller_knowing_its_shape()
    {
        var tree = new ChargeGroup("root")
            .Add(new ChargeLine("a", 10m))
            .Add(new ChargeGroup("nested")
                .Add(new ChargeLine("b", 5m))
                .Add(new ChargeLine("c", 2.50m)));

        tree.Total().ShouldBe(17.50m);
    }

    [Fact]
    public void Flyweight_allocates_one_object_per_distinct_code()
    {
        var factory = new ProcedureCodeFactory();

        for (var i = 0; i < 1_000; i++) factory.Get(i % 2 == 0 ? "A4253" : "E0607");

        factory.Requests.ShouldBe(1_000);
        factory.DistinctObjects.ShouldBe(2);
    }

    [Fact]
    public void Proxy_defers_creation_until_the_first_call()
    {
        var lazy = new LazyDocumentProxy(() => new BlobDocumentStore());

        lazy.RealObjectCreated.ShouldBeFalse();
        lazy.Download(Guid.CreateVersion7());
        lazy.RealObjectCreated.ShouldBeTrue();
    }

    [Fact]
    public void Strategy_selects_by_plan_and_fails_loudly_on_an_unknown_one()
    {
        var context = new PricingContext([new FlatRate(), new Tiered(), new Contracted()]);

        context.Price("flat", "A4253", 100).ShouldBe(1_000m);
        context.Price("tiered", "A4253", 100).ShouldBe(700m);
        context.Price("contracted", "A4253", 100).ShouldBe(42m);

        Should.Throw<NotSupportedException>(() => context.Price("capitation", "A4253", 1));
    }

    [Fact]
    public void Observer_stops_notifying_after_the_subscription_is_disposed()
    {
        var workflow = new ClaimWorkflow();
        var audit = new AuditObserver();
        var subscription = workflow.Subscribe(audit);

        workflow.Submit("CLM-001", 10m);
        subscription.Dispose();
        workflow.Submit("CLM-002", 10m);

        audit.Seen.Count.ShouldBe(1);
    }

    [Fact]
    public void Command_undo_restores_the_previous_state()
    {
        var draft = new ClaimDraft();
        var editor = new ClaimEditor(draft);

        editor.Do(new AddLineCommand("A4253", 100));
        editor.Do(new ChangePayerCommand("PBM-07"));

        editor.Undo();
        draft.PayerCode.ShouldBe("PBM-01");

        editor.Undo();
        draft.Lines.ShouldBeEmpty();

        editor.Undo().ShouldBeFalse();
    }

    [Fact]
    public void ChainOfResponsibility_routes_by_amount()
    {
        static string Approve(decimal amount)
        {
            var request = new ClaimRequest("CLM-X", amount, "test");
            var chain = new ValidationHandler();
            chain.Then(new AutoApprovalHandler()).Then(new SupervisorHandler()).Then(new DirectorHandler());
            chain.Handle(request);
            return string.Join(" ", request.Trail);
        }

        Approve(100m).ShouldContain("approved by auto");
        Approve(3_000m).ShouldContain("approved by supervisor");
        Approve(50_000m).ShouldContain("approved by director");
        Approve(0m).ShouldContain("rejected");
    }

    [Fact]
    public void State_refuses_transitions_the_current_state_does_not_allow()
    {
        var claim = new ClaimContext("CLM-001");

        claim.Pay();
        claim.State.ShouldBeOfType<DraftState>();

        claim.Submit();
        claim.Pay();
        claim.State.ShouldBeOfType<PaidState>();

        claim.Void();
        claim.State.ShouldBeOfType<PaidState>();
    }

    [Fact]
    public void Memento_restores_without_the_caretaker_seeing_inside()
    {
        var claim = new EditableClaim("PBM-01");
        claim.AddLine("A4253", 100);

        var snapshot = claim.Save();

        claim.ChangePayer("PBM-07");
        claim.AddLine("E0607", 1);

        claim.Restore(snapshot);

        claim.PayerCode.ShouldBe("PBM-01");
        claim.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public void Interpreter_evaluates_a_parsed_filter_expression()
    {
        var expression = FilterParser.Parse("payer=PBM-01 AND amount>1000 AND NOT status=Paid");

        expression.Interpret(new ClaimFacts("PBM-01", "Submitted", 4_200m)).ShouldBeTrue();
        expression.Interpret(new ClaimFacts("PBM-01", "Paid", 4_200m)).ShouldBeFalse();
        expression.Interpret(new ClaimFacts("PBM-01", "Submitted", 80m)).ShouldBeFalse();
        expression.Interpret(new ClaimFacts("PBM-07", "Submitted", 9_000m)).ShouldBeFalse();
    }

    [Fact]
    public void Visitor_adds_operations_without_touching_the_nodes()
    {
        PatternsLab.Patterns.Behavioural.ClaimComponent[] components =
        [
            new ClaimLineNode("A4253", 100, 0.42m),
            new ClaimLineNode("E0607", 1, 38.00m)
        ];

        var describe = new DescribingVisitor();
        components.Select(c => c.Accept(describe)).ShouldContain("E0607 x1 @ 38.00");

        var total = new TotallingVisitor();
        decimal running = 0;
        foreach (var component in components) running = component.Accept(total);

        running.ShouldBe(80.00m);
    }

    [Fact]
    public void Bridge_lets_report_and_renderer_vary_independently()
    {
        new SummaryReport(new PlainTextRenderer()).Render(3, 100m, "PBM-01").First().ShouldStartWith("== ");
        new SummaryReport(new HtmlRenderer()).Render(3, 100m, "PBM-01").First().ShouldStartWith("<h2>");
        new DetailedReport(new CsvRenderer()).Render(3, 100m, "PBM-01").Count().ShouldBe(6);
    }

    [Fact]
    public void Mediator_delivers_without_the_modules_referencing_each_other()
    {
        var mediator = new ClaimMediator();
        var intake = new IntakeModule();
        var pricing = new PricingModule();
        var billing = new BillingModule();

        mediator.Register(intake);
        mediator.Register(pricing);
        mediator.Register(billing);

        intake.Accept("CLM-001");

        billing.Inbox.ShouldContain(m => m.Contains("priced at 42.50"));
    }

    [Fact]
    public void TemplateMethod_keeps_the_step_order_fixed_across_subclasses()
    {
        var steps = new List<string>();

        new CsvClaimImport().Run("A4253,100\nBAD,0", steps.Add);

        steps.Where(s => s.StartsWith("[csv]")).Select(s => s.Split("] ")[1]).Take(3)
             .ShouldBe(["parse", "validate", "persist"]);
    }

    [Fact]
    public void Prototype_deep_clone_does_not_share_the_mutable_list()
    {
        var template = new ClaimTemplate("PBM-01", "tiered", ["A4253"], false);
        var clone = template.DeepClone();

        ReferenceEquals(template.DefaultProcedureCodes, clone.DefaultProcedureCodes).ShouldBeFalse();
    }

    [Fact]
    public void AbstractFactory_never_mixes_two_providers()
    {
        var sqlServer = new SqlServerFactory();
        var sqlite = new SqliteFactory();

        sqlServer.CreatePagingDialect().Page(50, 25).ShouldContain("OFFSET 50 ROWS");
        sqlite.CreatePagingDialect().Page(50, 25).ShouldContain("LIMIT 25 OFFSET 50");
    }
}
