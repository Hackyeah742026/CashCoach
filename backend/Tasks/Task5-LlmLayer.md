# Task 5: LLM layer (PDF B8, B18 to B21, P6)

Goal: Gemini-powered chat and captions, with every number verified against computed facts.

## Steps
1. `ILlmClient` (Core) and `GeminiClient` (Infrastructure, `Google.GenAI`)
   - `CompleteAsync(system, messages, tools, jsonMode)`.
   - Temperature 0.3 for text, 0 for JSON; max 400 output tokens for chat; 15 s timeout; 1 retry on network error.
   - Key `GEMINI_API_KEY` from `.env` via `IConfiguration`; never logged.
2. Prompts as `.md` files in `Ai/Prompts/` (embedded resources): system prompt with `{lang}` and `{today}`, categorization prompt, caption prompt.
3. `ToolRegistry`
   - 9 tools: `get_balance`, `get_spending`, `get_upcoming_payments`, `forecast_until_payday`, `simulate_purchase`, `simulate_change`, `list_subscriptions`, `get_goals`, `create_goal`.
   - Schema for the model plus C# implementation calling Core services.
   - `user_id` is injected by the server, never chosen by the model.
4. `NumberValidator`
   - Regex for Polish formats (`1 234,50`, `1234.5`, `24.`, `30%`).
   - Allowed set flattened from facts; ignore 0-10 counts and years.
   - Tolerance 0.01 for money, +/-1 when rounded to whole zl.
   - Log every failure.
5. `ChatAgent.RunChatAsync(userId, conversationId, message)`
   - Last 10 messages, max 5 tool rounds, collect `facts` per call.
   - Validate; on failure retry up to 2 times; then template answer (`fallback: true`).
6. Endpoints: `POST /chat`, `GET /chat/{conversation_id}`, `GET /chat/suggestions`. Rate limit 20 requests/min per user (`AddRateLimiter`, partitioned by `X-User-Id`).
7. Other LLM uses (all with template fallback)
   - Wrapped captions and opportunity explanations, validated and cached per user and month.
   - LLM categorization fallback in `Categorizer`.
8. Privacy: send only aggregates and tool results; strip IBANs, names and addresses before any Gemini call.
9. Test 20 tricky questions (investment advice, loans, made-up numbers) and tune the prompt.

## Done when
- 10 test questions return verified answers with facts attached.
- Validator passes 15 pass/fail test answers.
- With no API key or on failure, template fallback answers work.
- No key value appears in logs, code or docs.
