# 30 - Proof of Concept

# Problem
We have basic architecture/project structure in place. Now I would like to do a Poc (proof of concept). So it should be a single example, happy path scenario, where I can init leontes from cli, then all app will be set up in a basic way, then I can use leontes cli to send a message, message should be processed, saved if needed and my basic microsoft agent framework will process that, do the action of example tool and respond. Architecture, db, message queing, agent framework should use real structure. Let's skip building the tools (tool forge) and proacrtiveness for later. But for now we just need to leave some comments, placeholder code. Now let's focus on manually created example tool and no proactiveness. The naming, condig stadards should be real and pro so stick to our AI (claude, copilot setup).

# Prequisities
- Db should be set up in docker as the rest of the project if it is not there.
- If you need to queue messages do not use any external dependencies, use memory or postgres.
- Skip memory of leontes for now.

# Rules
Please use the config defined in AI files (copilot-instruction, claude, rules, instructions etc.) Do not make assumptions, ask instead. Firstly, prepare a plan. Do the best you can, to make great solutions.
