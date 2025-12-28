#!/bin/bash
# Run all tests
cd "$(dirname "$0")"
dotnet test MyBlog.slnx -v normal
