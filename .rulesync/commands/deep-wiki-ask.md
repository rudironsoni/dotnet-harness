---
name: deep-wiki-ask
description: '$1'
targets: ['*']
version: '0.0.1'
author: 'dotnet-agent-harness'
---

# /deep-wiki:ask

Ask a question about the repository

## Usage

````bash
/deep-wiki:ask <question>
```text

## Parameters

- `question`: Natural language question about the codebase

## Description

Answers questions grounded in actual source code with citations.

## Examples

```bash
/deep-wiki:ask What database migrations exist?
/deep-wiki:ask How is dependency injection configured?
/deep-wiki:ask Where is user authentication handled?
```text
````
