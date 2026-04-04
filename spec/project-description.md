# Project Description

## Vision

A personal AI assistant that communicates through multiple channels (CLI, Signal, voice), processes messages asynchronously using LLMs, and builds long-term memory to provide increasingly personalized and context-aware responses.

## Target Users

- **Primary:** The project owner — a single user who wants a unified AI assistant across devices and channels.

## Core Problem

Existing AI assistants are stateless per-session and locked to a single interface. There's no way to message your assistant from Signal, pick up the conversation from CLI, and have it remember context from last week — without paying for a hosted service that owns your data.

## Key Outcomes

- Seamless conversation across CLI and Signal
- Persistent memory that improves responses over time
- Full ownership of data and configuration
- Voice interaction as an enhancement layer

## Scope Boundaries

- **In scope (v1):** CLI chat, Signal integration, async message processing loop, LLM orchestration, vector memory, configuration management, user auth.
- **Out of scope (v1):** Multi-user support, mobile app, proactive notifications, third-party integrations beyond Signal.
