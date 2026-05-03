# TrueRAG Product Vision & Differentiators

This document outlines the core vision of TrueRAG. It is designed to act as the reference architecture and marketing showcase for **TrueParser**.

While standard RAG APIs fail because they rely on dumb text splitting (which destroys tables and paragraphs), TrueRAG exploits TrueParser's flawless high-fidelity output. 

Here are the standout features that make TrueRAG unique, including advanced capabilities that mainstream frameworks (like LangChain or LlamaIndex) fundamentally cannot do out-of-the-box:

## 🌟 1. Structural Context Expansion (Instead of Token Windowing)
* **The Problem:** Standard RAG grabs a chunk and says, "give me 200 tokens before and after" to get context. This often grabs the end of an unrelated section, confusing the LLM.
* **The Killer Feature:** Because TrueParser understands document hierarchy, TrueRAG supports *Structural Expansion*. If the vector search hits a paragraph inside `Section 3.2: Risk Factors`, the Retrieval Engine doesn't grab adjacent tokens; it dynamically pulls the entire `Section 3.2` node from CrateDB.
* **Marketing Pitch:** *"True RAG doesn't chunk blindly. Because TrueParser maps document taxonomy, our RAG API retrieves logical sections, not arbitrary token windows, virtually eliminating hallucinations."*

## 🌟 2. High-Fidelity Provenance & Visual Citation
* **The Problem:** Standard RAG simply says "Here is the answer (Source: Doc_A)".
* **The Killer Feature:** Because TrueParser outputs exact bounding boxes and structural coordinates, the TrueRAG API returns rich provenance metadata with every LLM response.
* **Implementation:** The Retrieval Engine passes bounding box data to the Conversation Module. The API response includes the exact Page, X/Y coordinates, and Table Cell of the source data.
* **Marketing Pitch:** *"Don't just cite the document; pinpoint the exact cell in the exact table on page 42 where the LLM found the answer."*

## 🌟 3. "Table-Aware" Hybrid Search & Injection
* **The Problem:** LLMs hallucinate badly on tables because standard parsers turn them into scrambled text.
* **The Killer Feature:** TrueRAG stores TrueParser's tables as JSON/Markdown in a dedicated CrateDB `OBJECT` column. When a user asks a question requiring tabular data, Hybrid Search retrieves the structured JSON of the table and injects it directly into the LLM context as a perfect Markdown table or JSON array.
* **Marketing Pitch:** *"Financial and scientific RAG requires perfect table fidelity. Our RAG API preserves the row/column axis context all the way to the LLM prompt."*

## 🌟 4. Conversation "Memory" with Document Scoping
* **The Problem:** Most RAG memory just appends previous chat messages. If the user shifts context, the LLM gets confused.
* **The Killer Feature:** In the Conversation/Memory module, TrueRAG tracks which documents and sections the user is actively asking about. If they ask, "What about Q3?", the API automatically applies a CrateDB metadata filter (`WHERE document_type = 'Quarterly Report' AND quarter = 'Q3'`) based on the active conversation state.
* **Marketing Pitch:** *"Stateful Document RAG: The API remembers not just what you said, but exactly which sections of which documents you were looking at."*

## 🌟 5. Analytical RAG (Deterministic Math over Unstructured Tables)
* **The Problem:** If you ask a standard RAG system, "What is the total sum of Q3 revenue across these 5 reports?", it feeds the text to an LLM, which tries to do the math and hallucinates.
* **The Killer Feature:** Because TrueParser extracts tables cleanly and CrateDB/Postgres natively support querying JSON `OBJECT`s, TrueRAG can dynamically route tabular/aggregation questions into actual **SQL queries over the extracted tables** rather than vector searches.
* **Marketing Pitch:** *"Stop asking language models to do math. TrueRAG turns unstructured PDF tables into queryable SQL objects instantly, providing 100% deterministic calculations across thousands of documents."*

## 🌟 6. Cross-Reference Resolution ("Multi-Hop Document Linking")
* **The Problem:** A retrieved chunk says "As detailed in Appendix B, Table 4..." but the standard vector search didn't retrieve Table 4 because its text didn't semantically match the user's prompt. The LLM is left blind.
* **The Killer Feature:** Because TrueParser maintains structural fidelity, TrueRAG detects internal document references during ingestion. If a retrieved paragraph references another section or figure, the Retrieval Engine automatically performs a "Hop" and fetches the referenced node to include in the context.
* **Marketing Pitch:** *"TrueRAG reads like a human. If a retrieved paragraph says 'See Figure 2', the engine automatically fetches Figure 2 for the LLM's context window."*

## 🌟 7. Structural Diffing & Version-Aware RAG
* **The Problem:** If a user asks "What changed in the termination clause between the 2022 and 2023 contracts?", standard RAG retrieves completely unaligned, broken chunks of text, making comparison impossible for the LLM.
* **The Killer Feature:** Because documents are parsed as hierarchical trees (Document -> Section -> Clause), TrueRAG can perform a structural "diff" directly in the database. The API retrieves the 2022 and 2023 versions of exactly the same logical node, highlights the diff, and feeds *only the changes* to the LLM.
* **Marketing Pitch:** *"Flawless document comparison. TrueRAG aligns documents by their logical structure, giving the LLM an exact diff of specific clauses across different versions."*

## 🌟 8. Dual-Layer Confidence Scoring
* **The Problem:** Business users cannot trust LLMs because standard RAG APIs provide no indication of whether the system is "guessing" or "certain".
* **The Killer Feature:** TrueRAG calculates a deterministic **Retrieval Confidence** (from CrateDB vector distances and full-text matching) and combines it with a prompted **LLM Certainty Score**. If the required data is not found in the TrueParser context, the API explicitly returns a low confidence score rather than hallucinating an answer.
* **Marketing Pitch:** *"Enterprise Trust by Default. TrueRAG provides a mathematical confidence score for every answer it generates, allowing UI applications to flag uncertain answers automatically."*
