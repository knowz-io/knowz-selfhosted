# =============================================================================
# Pester tests for approve-front-door-pl.ps1 (NodeID INFRA_FrontDoorPlApproval)
# =============================================================================
# Validates:
#   1. Script parses without syntax errors.
#   2. Mandatory ResourceGroup parameter declared.
#   3. Optional CaeName + TimeoutSeconds parameters declared with expected defaults/range.
#   4. Script references expected az subcommands for listing and approving PE connections.
#   5. Final validation rejects any non-Approved state.
#
# Compatible with Pester 3.4.0 (Windows PowerShell 5.1) and Pester 5.x.
# =============================================================================

$scriptPath = Join-Path $PSScriptRoot "..\post-deploy\approve-front-door-pl.ps1"

Describe "approve-front-door-pl.ps1 - INFRA_FrontDoorPlApproval" {

    Context "Script file exists and parses" {
        It "Should_ExistAtExpectedPath_WhenTestsRun" {
            Test-Path $scriptPath | Should Be $true
        }

        It "Should_ParseWithoutErrors_WhenAstLoaded" {
            $parseErrors = $null
            [System.Management.Automation.Language.Parser]::ParseFile(
                $scriptPath, [ref]$null, [ref]$parseErrors) | Out-Null
            ($null -eq $parseErrors -or $parseErrors.Count -eq 0) | Should Be $true
        }
    }

    $tokens = $null
    $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        $scriptPath, [ref]$tokens, [ref]$errors)
    $scriptText = Get-Content $scriptPath -Raw

    Context "VERIFY 1: Parameter contract" {
        $paramBlock = $ast.Find(
            { param($n) $n -is [System.Management.Automation.Language.ParamBlockAst] }, $true)
        $paramNames = @()
        if ($paramBlock) {
            $paramNames = $paramBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath }
        }

        It "Should_DeclareResourceGroup_WhenParamBlockParsed" {
            $paramNames -contains "ResourceGroup" | Should Be $true
        }
        It "Should_DeclareCaeName_WhenParamBlockParsed" {
            $paramNames -contains "CaeName" | Should Be $true
        }
        It "Should_DeclareTimeoutSeconds_WhenParamBlockParsed" {
            $paramNames -contains "TimeoutSeconds" | Should Be $true
        }

        It "Should_MarkResourceGroupMandatory_WhenParameterAttributesParsed" {
            $rg = $paramBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq "ResourceGroup" }
            $mandatoryAttr = $rg.Attributes | Where-Object {
                $_ -is [System.Management.Automation.Language.AttributeAst] -and
                $_.TypeName.Name -eq "Parameter"
            }
            ($mandatoryAttr.NamedArguments | Where-Object { $_.ArgumentName -eq "Mandatory" }).Argument.Extent.Text `
                | Should Be '$true'
        }

        It "Should_DefaultTimeoutTo180Seconds_WhenParameterDefaultParsed" {
            $scriptText -match '\[int\]\s*\$TimeoutSeconds\s*=\s*180' | Should Be $true
        }
    }

    Context "VERIFY 2: Script references required az subcommands" {
        It "Should_CallPrivateEndpointConnectionList_WhenDiscoveringPending" {
            $scriptText -match "private-endpoint-connection.*list" | Should Be $true
        }

        It "Should_CallPrivateEndpointConnectionApprove_WhenApprovingPending" {
            $scriptText -match "private-endpoint-connection.*approve" | Should Be $true
        }

        It "Should_DiscoverContainerAppsEnvironment_WhenCaeNameOmitted" {
            $scriptText -match "containerapp\s+env\s+list" | Should Be $true
        }
    }

    Context "VERIFY 3: Traffic-safety gates" {
        It "Should_RejectFinalStateContainingPending_WhenValidating" {
            $scriptText -match "Pending\|Disconnected\|Rejected" | Should Be $true
        }

        It "Should_ThrowWhenNoPendingConnectionsFound_BeforeTimeout" {
            $scriptText -match "No Pending Front Door private-link connections appeared" | Should Be $true
        }

        It "Should_PollWithStartSleep_WhileWaitingForPending" {
            $scriptText -match "Start-Sleep\s+-Seconds" | Should Be $true
        }
    }

    Context "VERIFY 4: Exits non-zero on failure paths" {
        It "Should_SetErrorActionPreferenceToStop_WhenScriptLoads" {
            $scriptText -match "\`$ErrorActionPreference\s*=\s*'Stop'" | Should Be $true
        }

        It "Should_ThrowOnAzCliNonZeroExit_WhenInvokeAzCliFails" {
            $scriptText -match "throw ""az.*failed" | Should Be $true
        }
    }
}
