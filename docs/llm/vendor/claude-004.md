02
35

this repository is public and available free of cost 
replace the readme file with a better version when delivering changes 
always make sure this readme is up to date with the latest code changes where possible 
Use your best judgment
If only a few files (under five or there about) need to change, give me the full path of the files that need to change and the FULL file 
if there are a lot of files that need to change 
    Delivery
    Whole project as one tar.gz, unambiguous file placement
    ONLY files that change need to be included in the tar zip
    as I will put it on top of the existing repository
    Any files to delete on updates called out explicitly
No bespoke sed/patch scripts — full files only for anything that changes
Docs updated alongside code changes
Low verbosity, no code comments
I can't emphasize this 
NO CODE COMMENTS 
the focus for this prompt is markdown 
for example, looks like we never implemented markdown tables? 
How can we implement markdown tables so we can use markdown in blog posts? 

for example, here is a blog post body that uses markdown tables 
different tables will have different sizes, number of rows and columns, etc 
is it possible to implement this in a robust, responsive way? 
if so, please implement fully 
if not, please tell me why it is not possible 

```markdown
# Grants.gov Explained: Building Blocks, Lifecycle, and the Limits of What the Public Can See

> **Read this first.** This article is built only from publicly available sources: official agency web pages, published regulations, open-source repositories, and a small number of secondary explainers (named in the sources section). It is not based on insider knowledge. The public view of the federal grants system is partial. Internal agency procedures, reviewer deliberations, system internals, and non-public data are not visible to outsiders, and no outsider, including the author, can claim to comprehend the full picture. Systems and policies here are also actively changing, so treat everything below as a snapshot as of October 26, 2026, and verify against the linked sources before relying on it. Claims that could not be tied to a public source are marked **[Unverified]** or **[Inference]** in the text.

## 1. What is Grants.gov?

Grants.gov is the U.S. government's central website where federal grant-making agencies announce funding opportunities and where applicants find and apply for them. Its own materials describe it as bringing awarding agencies and applicants together and following a lifecycle of creating the opportunity, applying, deciding awards, and carrying out the award ([Grants.gov lifecycle](https://grants.gov/learn-grants/grants-101/the-grant-lifecycle)). The opportunities listed there are grants and cooperative agreements ([Grants.gov Grants 101](https://www.grants.gov/learn-grants/grants-101.html)).

The Department of Health and Human Services (HHS) has been the managing partner of the program since at least the mid-2000s ([2004 archived agency page](https://webharvest.gov/peth04/20041015015750/http:/www.grants.gov/GrantingAgencies)), and today's site footer and the modernization effort (below) still sit under HHS ([Thompson Grants summary](https://www.thompsongrants.com/editorial-commentary/sneak-preview-pilot-testing-under-way-at-simplergrantsgov)).

Which agencies use it? A 2008 federal performance page said all 26 grant-making agencies used it ([OMB archive](https://georgewbush-whitehouse.archives.gov/omb/egov/c-7-14-grants.html)). Grants.gov's current agency page lists agencies such as AmeriCorps, USDA, Commerce, Defense, Education, Energy, HHS, Justice, Labor, NASA, NSF, SBA and others ([Grants.gov agency list](https://www.grants.gov/learn-grants/grant-making-agencies.html)). I did not find a current official count, so I am not stating one.

## 2. The building blocks (glossary)

| Term | Plain-language meaning | Source |
|---|---|---|
| **Grant / cooperative agreement** | The two kinds of assistance opportunities Grants.gov lists. | [Grants 101](https://www.grants.gov/learn-grants/grants-101.html) |
| **Notice of Funding Opportunity (NOFO)** | The public document in which an agency announces it intends to award discretionary grants or cooperative agreements, usually through competition. | [NIH glossary](https://www.ninds.nih.gov/glossary?title=NOFO) |
| **Solicitation** | An informal synonym. NIH's glossary says a NOFO may also be called a funding opportunity announcement, program announcement, request for applications, notice of funding availability, or "solicitation." NSF's own documents use "program solicitation." In *procurement* (contracts), "solicitation" has a different home: contract opportunities are posted on SAM.gov rather than Grants.gov. | [NIH glossary](https://www.ninds.nih.gov/glossary?title=NOFO), [NSF PAPPG](https://www.nsf.gov/policies/pappg/24-1/ch-1-pre-submission), [UNH sponsor guide](https://www.unh.edu/research/research/research-development-resources/find-funding/sponsor-types) |
| **Forecast** | A planned opportunity with actual or estimated dates and funding levels; it can change depending on congressional appropriations. | [HHS](https://www.hhs.gov/guidance/document/finding-funding-opportunities) |
| **Synopsis** | The summary record of an opportunity posted on Grants.gov; the regulation requires summary information to appear ahead of the full text. | [HHS](https://www.hhs.gov/guidance/document/finding-funding-opportunities), [2 CFR 200.204 (earlier text)](https://codes.findlaw.com/cfr/title-2-grants-and-agreements/cfr-sect-2-200-204/) |
| **Application (package)** | The forms and attachments an applicant submits through Grants.gov, which an agency later retrieves. | [Grants.gov lifecycle](https://grants.gov/learn-grants/grants-101/the-grant-lifecycle) |
| **Proposal** | The narrative and project ideas inside an application. Grants.gov's pre-award page lists "writing your proposal" as one step inside "completing the application." Agencies such as NSF use "proposal" for the whole submission. **[Inference]** Usage varies by agency; I found no single official definition separating the two. | [Pre-award phase](https://www.grants.gov/learn-grants/grants-101/pre-award-phase.html), [NSF PAPPG](https://www.nsf.gov/policies/pappg/24-1/ch-1-pre-submission) |
| **Letter of intent (LOI)** | A short, early document describing the project so the funder can judge fit; the funder may then invite a full application. | [California Grants Portal glossary](https://www.grants.ca.gov/glossary) (a state portal, used here only for the general definition) |
| **Pre-proposal / preliminary proposal** | A shorter first-round submission some programs require before the full proposal. NSF defines "preliminary proposals" in its guide and says the program solicitation specifies when they are used. | [NSF PAPPG 2022](https://www.nsf.gov/pubs/policydocs/pappg22_1/pappg_1.jsp) |
| **Award / Notice of Award (NOA)** | The NOA is the official, legally binding issuance of the award; accepting the grant obligates the recipient to its terms. | [Award phase](https://www.grants.gov/learn-grants/grants-101/award-phase) |
| **SAM.gov registration, UEI** | Applicants register in SAM.gov, which supplies the Unique Entity Identifier used when adding an organization profile to a Grants.gov account. | [Pre-award phase](https://www.grants.gov/learn-grants/grants-101/pre-award-phase.html), [MIT guide](https://kc.mit.edu/document/obtaining-grantsgov-account-for-workspace-january-2024) |
| **Login.gov** | The shared federal sign-in service; Grants.gov users link Login.gov to their account. | [Northeastern notice](https://res.research.northeastern.edu/sign-in-to-grants-gov-with-your-login-gov-credentials/) |
| **AOR (Authorized Organization Representative)** | The person at an organization who can actually submit. NIH calls the equivalent role a signing official in its own system. | [NIH](https://grants.nih.gov/grants/tracking_application.htm) |
| **Workspace** | Grants.gov's shared online environment where members of an organization collaboratively complete forms for one application. | [Grants.gov training](https://grants.gov/applicants/applicant-training) |
| **Assistance Listing** | The catalog numbering of federal assistance programs, which replaced the old CFDA numbers. | [UNH sponsor guide](https://www.unh.edu/research/research/research-development-resources/find-funding/sponsor-types) |

Federal regulation (2 CFR 200.204) requires agencies to announce openly competed funding opportunities publicly, says they may offer pre-application technical assistance, and says opportunities should generally be open at least 60 calendar days ([Cornell LII](https://www.law.cornell.edu/cfr/text/2/200.204)).

## 3. The lifecycle

Grants.gov divides the life of a grant into three phases ([lifecycle page](https://grants.gov/learn-grants/grants-101/the-grant-lifecycle)):

**Pre-award**
1. The agency posts the opportunity (sometimes earlier as a forecast).
2. Applicants search for it, register (Login.gov, SAM.gov, Grants.gov), and prepare an application ([registration order per NEA](https://www.arts.gov/sites/default/files/Organization-Registration-4.pdf); [Grants.gov pre-award page](https://www.grants.gov/learn-grants/grants-101/pre-award-phase.html)).
3. An AOR submits through Grants.gov; the agency later retrieves it and the applicant is emailed ([lifecycle page](https://grants.gov/learn-grants/grants-101/the-grant-lifecycle)). Grants.gov runs basic cross-agency checks and assigns a tracking number, and the agency then applies its own business rules ([NIH description](https://grants.nih.gov/grants/tracking_application.htm)).
4. The agency screens for completeness (incomplete applications are likely to be rejected) and then reviews ([pre-award page](https://www.grants.gov/learn-grants/grants-101/pre-award-phase.html)).

**Award**
5. Federal staff make recommendations through several levels of review, and the final decision rests with agency staff holding legal authority. A Notice of Award follows ([award phase](https://www.grants.gov/learn-grants/grants-101/award-phase)).

**Post-award**
6. The recipient carries out the project and reports; the agency's grants management officer oversees compliance ([lifecycle page](https://grants.gov/learn-grants/grants-101/the-grant-lifecycle)).
7. Closeout: Grants.gov says final financial and performance reports are due within 90 days of the award ending, citing section 200.343 ([post-award page](https://grants.gov/learn-grants/grants-101/post-award-phase.html)). **Note:** a secondary 2 CFR guide cites closeout at 200.344 ([Witt O'Brien's guide](https://government.wittobriens.com/hubfs/Guide%20to%20Federal%20Grants/The%20Full%20Federal%20Grant%20Lifecycle.pdf?hsLang=en)), which suggests the section was renumbered. I did not confirm the current numbering against the eCFR.

### Letters of intent and pre-proposals in the lifecycle

These are optional or required *per program*, not universal. For NSF, the program solicitation states whether a letter of intent or preliminary proposal is required ([NSF PAPPG](https://www.nsf.gov/policies/pappg/24-1/ch-1-pre-submission)), and NSF says LOIs must be prepared in Research.gov and cannot be submitted in Grants.gov ([Research.gov](https://www.research.gov/research-web/content/aboutpsm)). A university training deck describes LOIs as a way to gauge fit and preliminary proposals as a go/no-go screen ([OU slides](https://ou.edu/content/dam/research-norman/docs/training/NavigatingNSFProposalSubmissionProcessPart1.pdf), secondary). So the early rounds may live partly outside Grants.gov.

## 4. What technology does Grants.gov use?

Only part of this is publicly documented.

- **Machine-to-machine interface (documented).** Grants.gov offers an applicant System-to-System (S2S) interface using XML web services, with downloadable WSDLs, a Java reference implementation, hashing standards, and digital certificates ([Applicant S2S guide](https://grants.gov/system-to-system/applicant-system-to-system)). The site's navigation also lists a separate "RESTful APIs" section, which I did not read.
- **Sign-in (documented).** Login.gov ([Northeastern notice](https://res.research.northeastern.edu/sign-in-to-grants-gov-with-your-login-gov-credentials/)).
- **Legacy back end (partly documented).** A contractor case study says the current Grants.gov is built on a code base and architecture from 2002 ([Nava case study](https://www.navapbc.com/case-studies/modernizing-grants.gov-iteratively), secondary). An architecture decision record in the Simpler project notes that existing HHS websites and tools are built with Java ([Simpler wiki ADR](https://wiki.simpler.grants.gov/product/decisions/adr/2023-07-10-front-end-language)). **[Unverified]** I found no public source naming the legacy database or hosting.
- **Current front-end pages.** Asset paths on current Grants.gov pages contain `_nuxt`, which **[Inference]** suggests the Nuxt (Vue) framework; this is my reading of the page's source, not a published statement.
- **The modernization project, Simpler.Grants.gov (documented, open source).** Its public repository describes a Python/Flask API, a Next.js front end, and Terraform for AWS infrastructure ([HHS repository](https://github.com/hhs/grants-api)). It copies production Grants.gov data into its own system so it can work without affecting the live service ([Nava](https://www.navapbc.com/case-studies/modernizing-grants.gov-iteratively)). The project says a three-year effort began in October 2024 ([Thompson Grants](https://www.thompsongrants.com/editorial-commentary/sneak-preview-pilot-testing-under-way-at-simplergrantsgov)). A rollout date for retiring the classic system was not stated in the official materials I read; third-party blogs gave dates, which I excluded as unreliable.

## 5. What is `trainingapply.grants.gov`, and who can use it?

**What I could confirm directly:** requesting `https://trainingapply.grants.gov/` returns a short page that redirects to `https://www.training.grants.gov/`. I could not load the destination because the site blocks automated access, so I did not see what it displays.

**What the documentation says about the training environment.** Grants.gov's S2S testing page describes a "Grants.gov Training Environment" at `training.grants.gov` for testing an organization's system against the web services: retrieving opportunities, submitting an application, and listing submissions ([S2S testing](https://grants.gov/system-to-system/applicant-system-to-system/testing)). Its prerequisites are a user registered in the training environment with a submit role and an installed certificate. The S2S start page says to create an account and organization applicant profile to test at `training.grants.gov`, and that you will be prompted to create and link a "sandbox" account through Login.gov ([S2S guide](https://grants.gov/system-to-system/applicant-system-to-system)). A July 2025 release note adds that on the training site, the "New Simpler search" link points to production, so training users should use "Classic search" to see training opportunities ([release notes](https://a.storyblok.grants.gov/f/21/x/734389b340/release-notes_r2025-01-4-simpler.pdf)).

A vendor document shows who uses it in practice: a grant-software company says its training environment's submissions go to the Grants.gov test server, with different opportunity numbers than production ([Cayuse](https://support.cayuse.com/hc/en-us/articles/115013737588-Using-Your-Cayuse-424-Training-Environment), secondary).

**What I could not confirm:**
- **[Unverified]** Whether `trainingapply.grants.gov` is a distinct host for the applicant side of the training environment, or just a legacy alias. **[Inference]** Older docs used hostnames like `apply07` and `at07web`, so multiple host names appear to have existed over time ([archived testing page](https://cybercemetery.unt.edu/oilspill/20130213205425mp_/http://grants.gov/applicant_s2s/applicant-testing.jsp)).
- **[Unverified]** Any public statement that sets out exactly who is eligible for a training account.
- Login.gov's own sandbox documentation gives conflicting signals: one version says anyone can create a sandbox account on their own, another says agency partners with a .gov or .mil address can ([Login.gov developer docs](https://developers.login.gov/testing/)). Whether Grants.gov's training environment follows either rule is not stated in what I read.

## 6. Can outsiders build proof-of-concept apps against it? How open is the platform?

**Applying to build against the training sandbox.** I found no public application process, sponsorship program, or "apply to build a proof of concept" form for the training environment. The documented path is self-service: the S2S guide assumes your organization already has a Grants.gov account with the Expanded AOR role, a developer who knows XML web services, and a digital certificate ([S2S guide](https://grants.gov/system-to-system/applicant-system-to-system)). That reads as aimed at organizations that submit applications (or their software vendors), not hobbyist showcases. Whether an individual with no organization can get through the certificate and role steps is **[Unverified]**. Questions can go to Grants.gov support, which the S2S page points to.

**The genuinely open door: Simpler.Grants.gov.** This is explicitly public and open source:
- The code is open source and the roadmap is public ([Simpler process page](https://simpler.grants.gov/process)).
- A free API key (created from the developer page with a user account) lets you search opportunities and fetch details. Limits listed: keys unused for 30 days are disabled; default rates of 60 requests per minute and 10,000 per day; and **write operations are not supported**, so you cannot apply, post opportunities, or create organization projects through it ([developer page](https://simpler.grants.gov/developer); [API wiki](https://wiki.simpler.grants.gov/product/api)).
- The project has run public coding challenges (Fall 2024, Spring 2025) with published submissions and winners ([wiki](https://wiki.simpler.grants.gov/get-involved/community-events/fall-2024-coding-challenge)), which is a precedent for community showcases.

**Bottom line.** Reading public opportunity data and building tools on it: open. Submitting real applications programmatically: gated by registration, roles, and certificates. Training-environment access for demos: documented for applicant organizations; outsider eligibility not publicly spelled out.

## 7. Which agencies use it, and how do they differ?

Agencies post their opportunities on Grants.gov, but what happens before and after varies. These are examples, not a complete survey.

| Agency / office | What public sources show | Source |
|---|---|---|
| **NIH** | Opportunities are listed in Grants.gov and the NIH Guide. Applicants can submit through NIH's ASSIST, Grants.gov Workspace, or their own system. Grants.gov checks first, then NIH's eRA systems retrieve the application and check it against NIH rules; applicants must verify their application image in eRA Commons. | [NIH application process](https://grants.nih.gov/grants/how-to-apply-application-guide/forms-i/general/g.110-application-process.htm), [NIH tracking](https://grants.nih.gov/grants/tracking_application.htm) |
| **NSF** | Proposals go through Research.gov or Grants.gov unless a solicitation says otherwise; Grants.gov proposals are processed in Research.gov. Letters of intent are Research.gov only. | [NSF PAPPG](https://www.nsf.gov/policies/pappg/24-1/ch-1-pre-submission), [Research.gov](https://www.research.gov/research-web/content/aboutpsm) |
| **Dept. of Justice** | A two-step flow: an abbreviated application in Grants.gov, then entity onboarding and the full application in JustGrants. SAM.gov registration must be active. | [JustGrants training](https://justicegrants.usdoj.gov/taxonomy/term/application-submitter) |
| **HHS (ACF example)** | Posts forecasts and synopses on Grants.gov, the same place applications are submitted. | [HHS](https://www.hhs.gov/guidance/document/finding-funding-opportunities) |
| **Dept. of Education** | Routinely publishes grant announcements in the Federal Register as well as on Grants.gov. | [UNH sponsor guide](https://www.unh.edu/research/research/research-development-resources/find-funding/sponsor-types) (secondary) |
| **Grants-management systems (e.g., GrantSolutions)** | Used after award; a Dept. of Labor training deck says GrantSolutions does not replace Grants.gov. | [DOL slides](https://www.dol.gov/sites/dolgov/files/general/grants/grant-solutions/ETA-ODEP-WB_Grant-Recipient-Training-Presentation-Part-2.pdf) (secondary) |

The pattern: Grants.gov is a shared front door for finding opportunities and (usually) submitting, while each agency runs its own downstream systems, review rules, terminology, and sometimes its own submission portal. Even the same word ("proposal," "application," "solicitation") can mean slightly different things across them. I did not survey internal differences between offices within agencies; those are mostly not public.

## 8. What the public can't see

- How reviewers and agency staff weigh applications beyond published criteria.
- Internal system architecture, security controls, and operations of the legacy platform.
- Who exactly may obtain training-environment access, and under what approval.
- How much traffic, error, or rejection data exists beyond what is published.
- Any non-public roadmap decisions. The public Simpler roadmap and release notes are the best window, but they are a curated view.

## Sourcing transparency note

**Primary / official sources (strongest):** Grants.gov pages (lifecycle, award phase, post-award phase, S2S guides, release notes); the 2 CFR 200.204 text via Cornell LII; NSF's PAPPG and Research.gov; NIH and eRA pages; DOJ JustGrants pages; Simpler.Grants.gov's site, wiki, and public GitHub repository; Login.gov developer docs; an archived 2008 OMB performance page.

**Secondary sources (used with caution):** Nava's case study (a contractor's account of the project), Cayuse's vendor documentation, Thompson Grants, UNH and University of Oklahoma training materials, the California Grants Portal glossary (state, used for a general definition only), the NEA registration PDF, MIT's registration guide, Northeastern's Login.gov notice, Witt O'Brien's guide, and the DOL training deck. Anything relying only on these is labeled as secondary or flagged.

**Contested or uncertain items:** closeout section numbering (200.343 vs 200.344); whether `trainingapply.grants.gov` is a distinct supported host; who may get training accounts; the Login.gov sandbox eligibility language; the current count of participating agencies.

**Excluded:** several websites that appeared in searches and looked auto-generated or aggregated (including casrai.org pages and opengrants.io), plus mirror sites that copied Grants.gov content. I cited the Grants.gov originals instead. Their claims about Simpler.Grants.gov replacement dates were not used.

**Not verified because access was blocked or the page was unavailable:** the content of `https://www.training.grants.gov/` (automated access disallowed) and the current eCFR page for 2 CFR 200.344.

*Research for this article was done on October 26, 2026. Verify dates, rules, and hostnames before relying on them.*
```

