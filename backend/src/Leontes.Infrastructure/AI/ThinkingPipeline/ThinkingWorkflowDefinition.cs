using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Requests;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Microsoft.Agents.AI.Workflows;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

internal static class ThinkingWorkflowDefinition
{
    public static Workflow Build(
        PerceiveExecutor perceive,
        EnrichExecutor enrich,
        PlanExecutor plan,
        PlanResumeExecutor planResume,
        ExecuteExecutor execute,
        ReflectExecutor reflect)
    {
        var questionPort = ProactiveRequestPorts.Question;

        var builder = new WorkflowBuilder(perceive)
            .WithName("ThinkingPipeline")
            .WithDescription("Multi-stage cognitive pipeline: Perceive → Enrich → Plan → Execute → Reflect");

        // Main linear path
        builder.AddEdge(perceive, enrich);
        builder.AddEdge(enrich, plan);

        // Plan branches: ThinkingContext → Execute, QuestionRequest → QuestionPort
        builder.AddEdge<ThinkingContext>(plan, execute,
            condition: ctx => ctx is not null);
        builder.AddEdge<QuestionRequest>(plan, questionPort,
            condition: req => req is not null);

        // HITL resumption path: QuestionPort → PlanResume → Execute
        builder.AddEdge(questionPort, planResume);
        builder.AddEdge(planResume, execute);

        // Execute → Reflect
        builder.AddEdge(execute, reflect);

        // Only Reflect yields workflow output
        builder.WithOutputFrom(reflect);

        return builder.Build();
    }
}
