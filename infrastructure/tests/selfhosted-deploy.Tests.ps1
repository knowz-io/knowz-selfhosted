# =============================================================================
# Pester tests for selfhosted-deploy.ps1 (NodeID 1C)
# =============================================================================
# Validates:
#   1. New parameters exist (ExistingOpenAi/Vision/DocIntel Name + ResourceGroup)
#   2. Pre-deployment validation (Step 0.5) exists and checks paired name/RG
#   3. Bicep deployment body includes 6 new existing* parameters
#   4. RBAC retry loop replaces the 10-second sleep
#   5. Secret retrieval uses 3-tier pattern for OpenAI, Vision, DocIntel
#   6. Vision and DocIntel failures throw (instead of warnings)
#   7. Header display shows existing resource info
#
# Compatible with Pester 3.4.0 (PowerShell 5.1).
# =============================================================================

$scriptPath = Join-Path $PSScriptRoot "..\selfhosted-deploy.ps1"

Describe "selfhosted-deploy.ps1 - NodeID 1C (existing AI resources, RBAC retry)" {

    Context "Script file exists" {
        It "Should_ExistAtExpectedPath_WhenTestsRun" {
            Test-Path $scriptPath | Should Be $true
        }
    }

    # AST parse once; used by downstream tests
    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)
    $scriptText = Get-Content $scriptPath -Raw

    Context "VERIFY 1: Six new parameters exist in param block" {
        $paramBlock = $ast.ParamBlock
        if (-not $paramBlock) {
            # Top-level param block may live in the first script block statement
            $paramBlock = $ast.Find({ param($n) $n -is [System.Management.Automation.Language.ParamBlockAst] }, $true)
        }
        $paramNames = @()
        if ($paramBlock) {
            $paramNames = $paramBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath }
        }

        It "Should_DeclareExistingOpenAiName_WhenParamBlockParsed" {
            $paramNames -contains "ExistingOpenAiName" | Should Be $true
        }
        It "Should_DeclareExistingOpenAiResourceGroup_WhenParamBlockParsed" {
            $paramNames -contains "ExistingOpenAiResourceGroup" | Should Be $true
        }
        It "Should_DeclareExistingVisionName_WhenParamBlockParsed" {
            $paramNames -contains "ExistingVisionName" | Should Be $true
        }
        It "Should_DeclareExistingVisionResourceGroup_WhenParamBlockParsed" {
            $paramNames -contains "ExistingVisionResourceGroup" | Should Be $true
        }
        It "Should_DeclareExistingDocIntelName_WhenParamBlockParsed" {
            $paramNames -contains "ExistingDocIntelName" | Should Be $true
        }
        It "Should_DeclareExistingDocIntelResourceGroup_WhenParamBlockParsed" {
            $paramNames -contains "ExistingDocIntelResourceGroup" | Should Be $true
        }
    }

    Context "VERIFY 2: Pre-deployment validation (Step 0.5)" {
        It "Should_IncludeStep0_5Header_WhenScriptRendered" {
            ($scriptText -match "\[0\.5/7\]") | Should Be $true
        }
        It "Should_ThrowWhenExistingOpenAiNameWithoutResourceGroup_WhenValidationRuns" {
            # pattern: if ($ExistingOpenAiName -and -not $ExistingOpenAiResourceGroup) { throw ... }
            ($scriptText -match '\$ExistingOpenAiName\s+-and\s+-not\s+\$ExistingOpenAiResourceGroup') | Should Be $true
        }
        It "Should_ThrowWhenExistingVisionNameWithoutResourceGroup_WhenValidationRuns" {
            ($scriptText -match '\$ExistingVisionName\s+-and\s+-not\s+\$ExistingVisionResourceGroup') | Should Be $true
        }
        It "Should_ThrowWhenExistingDocIntelNameWithoutResourceGroup_WhenValidationRuns" {
            ($scriptText -match '\$ExistingDocIntelName\s+-and\s+-not\s+\$ExistingDocIntelResourceGroup') | Should Be $true
        }
        It "Should_CallAzCognitiveservicesAccountShow_ToValidateExistingOpenAi" {
            # Must validate accessibility before deployment
            ($scriptText -match 'az cognitiveservices account show --name \$ExistingOpenAiName') | Should Be $true
        }
        It "Should_CallAzCognitiveservicesAccountShow_ToValidateExistingVision" {
            ($scriptText -match 'az cognitiveservices account show --name \$ExistingVisionName') | Should Be $true
        }
        It "Should_CallAzCognitiveservicesAccountShow_ToValidateExistingDocIntel" {
            ($scriptText -match 'az cognitiveservices account show --name \$ExistingDocIntelName') | Should Be $true
        }
    }

    Context "VERIFY 3: Bicep deployment body includes 6 new existing* parameters" {
        It "Should_PassExistingOpenAiName_InDeploymentBody" {
            ($scriptText -match 'existingOpenAiName\s*=\s*@\{\s*value\s*=\s*\$ExistingOpenAiName\s*\}') | Should Be $true
        }
        It "Should_PassExistingOpenAiResourceGroup_InDeploymentBody" {
            ($scriptText -match 'existingOpenAiResourceGroup\s*=\s*@\{\s*value\s*=\s*\$ExistingOpenAiResourceGroup\s*\}') | Should Be $true
        }
        It "Should_PassExistingVisionName_InDeploymentBody" {
            ($scriptText -match 'existingVisionName\s*=\s*@\{\s*value\s*=\s*\$ExistingVisionName\s*\}') | Should Be $true
        }
        It "Should_PassExistingVisionResourceGroup_InDeploymentBody" {
            ($scriptText -match 'existingVisionResourceGroup\s*=\s*@\{\s*value\s*=\s*\$ExistingVisionResourceGroup\s*\}') | Should Be $true
        }
        It "Should_PassExistingDocIntelName_InDeploymentBody" {
            ($scriptText -match 'existingDocIntelName\s*=\s*@\{\s*value\s*=\s*\$ExistingDocIntelName\s*\}') | Should Be $true
        }
        It "Should_PassExistingDocIntelResourceGroup_InDeploymentBody" {
            ($scriptText -match 'existingDocIntelResourceGroup\s*=\s*@\{\s*value\s*=\s*\$ExistingDocIntelResourceGroup\s*\}') | Should Be $true
        }
    }

    Context "VERIFY 4: Key Vault RBAC retry loop replaces 10-second sleep" {
        It "Should_NotContainOriginalTenSecondSleep_InKeyVaultStep" {
            # Previous code had a hard-coded Start-Sleep -Seconds 10 immediately
            # before the KV verification. After the fix, the retry block uses 15s per attempt.
            # Ensure the single "Start-Sleep -Seconds 10" line used for KV is gone.
            $kvSection = $null
            if ($scriptText -match "(?s)Verifying Key Vault secrets.*?Set-Content") {
                $kvSection = $Matches[0]
            }
            $kvSection | Should Not BeNullOrEmpty
            ($kvSection -match 'Start-Sleep\s+-Seconds\s+10\b') | Should Be $false
        }
        It "Should_ContainKvRetryCounter_WhenRetryLoopImplemented" {
            ($scriptText -match '\$kvRetryCount') | Should Be $true
        }
        It "Should_ContainKvMaxRetries_WhenRetryLoopImplemented" {
            ($scriptText -match '\$kvMaxRetries') | Should Be $true
        }
        It "Should_ContainKvVerifiedFlag_WhenRetryLoopImplemented" {
            ($scriptText -match '\$kvVerified') | Should Be $true
        }
        It "Should_AssignKeyVaultSecretsOfficerRole_WhenVerificationFails" {
            ($scriptText -match 'Key Vault Secrets Officer') | Should Be $true
        }
    }

    Context "VERIFY 5: Secret retrieval uses 3-tier pattern" {
        It "Should_HaveOpenAiThreeTier_DeployedExistingExternal" {
            # Tier 2: ExistingOpenAiName branch with elseif
            ($scriptText -match 'elseif\s*\(\s*\$ExistingOpenAiName\s*\)') | Should Be $true
        }
        It "Should_HaveVisionThreeTier_DeployedExistingExternal" {
            ($scriptText -match 'elseif\s*\(\s*\$ExistingVisionName\s*\)') | Should Be $true
        }
        It "Should_HaveDocIntelThreeTier_DeployedExistingExternal" {
            ($scriptText -match 'elseif\s*\(\s*\$ExistingDocIntelName\s*\)') | Should Be $true
        }
        It "Should_QueryKeysForExistingOpenAi_WithSpecificResourceGroup" {
            ($scriptText -match 'az cognitiveservices account keys list --name \$ExistingOpenAiName --resource-group \$ExistingOpenAiResourceGroup') | Should Be $true
        }
        It "Should_QueryKeysForExistingVision_WithSpecificResourceGroup" {
            ($scriptText -match 'az cognitiveservices account keys list --name \$ExistingVisionName --resource-group \$ExistingVisionResourceGroup') | Should Be $true
        }
        It "Should_QueryKeysForExistingDocIntel_WithSpecificResourceGroup" {
            ($scriptText -match 'az cognitiveservices account keys list --name \$ExistingDocIntelName --resource-group \$ExistingDocIntelResourceGroup') | Should Be $true
        }
    }

    Context "VERIFY 6: Vision and DocIntel failures are errors (throw)" {
        It "Should_ThrowOnVisionKeyRetrievalFailure_FromDeployedResource" {
            # The deployed-resource branch for vision should throw, not just warn
            ($scriptText -match 'throw\s+"Failed to retrieve Vision key from deployed resource"') | Should Be $true
        }
        It "Should_ThrowOnDocIntelKeyRetrievalFailure_FromDeployedResource" {
            ($scriptText -match 'throw\s+"Failed to retrieve (Document Intelligence|Doc Intel|DocIntel) key from deployed resource"') | Should Be $true
        }
        It "Should_ThrowOnVisionKeyRetrievalFailure_FromExistingResource" {
            $needle = 'throw "Failed to retrieve key from existing Vision resource'
            $scriptText.Contains($needle) | Should Be $true
        }
        It "Should_ThrowOnDocIntelKeyRetrievalFailure_FromExistingResource" {
            # accept any of: Document Intelligence / Doc Intel / DocIntel naming
            $found =
                $scriptText.Contains('throw "Failed to retrieve key from existing Document Intelligence resource') -or
                $scriptText.Contains('throw "Failed to retrieve key from existing Doc Intel resource') -or
                $scriptText.Contains('throw "Failed to retrieve key from existing DocIntel resource')
            $found | Should Be $true
        }
    }

    Context "VERIFY 7: Header displays existing resource info" {
        It "Should_DisplayExistingOpenAiInHeader_WhenExistingOpenAiNameProvided" {
            ($scriptText -match 'OpenAI:\s+Existing\s+\(\$ExistingOpenAiName in \$ExistingOpenAiResourceGroup\)') | Should Be $true
        }
        It "Should_DisplayExistingVisionInHeader_WhenExistingVisionNameProvided" {
            ($scriptText -match 'Vision:\s+Existing\s+\(\$ExistingVisionName in \$ExistingVisionResourceGroup\)') | Should Be $true
        }
        It "Should_DisplayExistingDocIntelInHeader_WhenExistingDocIntelNameProvided" {
            ($scriptText -match 'Doc Intel:\s+Existing\s+\(\$ExistingDocIntelName in \$ExistingDocIntelResourceGroup\)') | Should Be $true
        }
    }

    Context "VERIFY 8: No syntactic regressions" {
        It "Should_ParseWithoutErrors_WhenAstParsed" {
            @($errors).Count | Should Be 0
        }
        It "Should_KeepExistingRequiredParameter_SqlPassword" {
            $paramBlock = $ast.Find({ param($n) $n -is [System.Management.Automation.Language.ParamBlockAst] }, $true)
            $names = $paramBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath }
            $names -contains "SqlPassword" | Should Be $true
        }
        It "Should_KeepExistingRequiredParameter_AdminPassword" {
            $paramBlock = $ast.Find({ param($n) $n -is [System.Management.Automation.Language.ParamBlockAst] }, $true)
            $names = $paramBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath }
            $names -contains "AdminPassword" | Should Be $true
        }
        It "Should_KeepSevenStepProgressMarkers_WhenScriptIntact" {
            # Steps 1/7 .. 7/7 still present in progress markers
            ($scriptText.Contains('[1/7]')) | Should Be $true
            ($scriptText.Contains('[2/7]')) | Should Be $true
            ($scriptText.Contains('[3/7]')) | Should Be $true
            ($scriptText.Contains('[4/7]')) | Should Be $true
            ($scriptText.Contains('[5/7]')) | Should Be $true
            ($scriptText.Contains('[6/7]')) | Should Be $true
            ($scriptText.Contains('[7/7]')) | Should Be $true
        }
    }
}
