---
title: Home
description: A dependency injection container for .NET that checks your wiring while you type, at startup and in CI.
layout: landing
---
<section class="mx-hero">
<div class="mx-hero-copy">
<span class="mx-pill">Version 1.0.0 &middot; .NET 8, 9 and 10</span>
<h1 class="mx-title">Know what is wrong before your users do.</h1>
<p class="mx-lead">Maxen.DI checks your wiring while you type, at startup and in CI. Then it resolves quickly and stays open to change.</p>
<div class="mx-install"><span>$</span>dotnet add package Maxen.DI</div>
<div class="mx-actions">
<a class="mx-btn mx-btn-primary" href="docs/getting-started.md">Get started</a>
<a class="mx-btn mx-btn-secondary" href="https://github.com/pfimax/maxen.di">View on GitHub</a>
</div>
</div>
<div class="mx-hero-visual">
<div class="mx-card">
<div class="mx-card-head"><span>Program.cs</span><span class="mx-warn-tag">1 warning</span></div>
<pre><code><span class="hljs-comment">// The report service reads the signed-in user, which lives for one request.</span>
builder.<span class="hljs-keyword">Register</span>&lt;<span class="hljs-type">ReportService</span>&gt;()
       .<span class="mx-squiggle">AsSingleton</span>();</code></pre>
<div class="mx-diagnostic"><b>MAXEN005</b><span>A singleton cannot depend on a scoped service. Change its lifetime.</span></div>
</div>
<div class="mx-card">
<div class="mx-card-head"><span>Terminal &middot; example output</span></div>
<pre><code><span class="mx-dim">$</span> maxen-di validate bin/Release/net10.0/MyApp.dll
<span class="mx-err">error</span>  ReportService needs CurrentUser (scoped)
       but is registered as a singleton
<span class="mx-dim">exit code 1</span></code></pre>
</div>
</div>
</section>
<div class="mx-facts">
<div class="mx-fact"><strong>3</strong><span>target frameworks</span></div>
<div class="mx-fact"><strong>4</strong><span>NuGet packages</span></div>
<div class="mx-fact"><strong>9</strong><span>analyzer diagnostics</span></div>
<div class="mx-fact"><strong>89</strong><span>Microsoft conformance tests passing</span></div>
</div>
<section class="mx-section">
<h2>Mistakes get found in three places</h2>
<p class="mx-section-lead">Pick the earliest one that fits. Each catches the same problems: a missing registration, a loop, a singleton holding a per-request service.</p>
<div class="mx-grid">
<div class="mx-way"><small>01 &middot; while you type</small><h3>Analyzers in your editor</h3><p>A missing registration, a captive dependency or a leaking disposable shows up as a warning. Many come with a one-click fix.</p><pre><code>MAXEN006  Constructor needs 'Database'</code></pre></div>
<div class="mx-way"><small>02 &middot; at startup</small><h3>Validation when you build</h3><p>Build() can refuse to start and list every problem at once, instead of failing on the first request that needs the service.</p><pre><code>builder.Options.ValidateOnBuild = true;</code></pre></div>
<div class="mx-way"><small>03 &middot; in CI</small><h3>A gate in your pipeline</h3><p>The maxen-di tool validates a built assembly and fails the build when the dependency graph changes by accident.</p><pre><code>maxen-di diff old.dll new.dll</code></pre></div>
</div>
</section>
<section class="mx-section">
<h2>Built to be changed</h2>
<div class="mx-grid">
<div class="mx-feature"><div class="mx-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3v4M12 17v4M3 12h4M17 12h4"/><circle cx="12" cy="12" r="3"/></svg></div><h3>Open all the way down</h3><p>Lifetimes, middleware, registration sources, interceptors and observers are plain interfaces. The built-in features use the same ones.</p></div>
<div class="mx-feature"><div class="mx-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8 7l-5 5 5 5M16 7l5 5-5 5M14 4l-4 16"/></svg></div><h3>Source-generated and AOT friendly</h3><p>Attributes become registrations, decorators and interceptor proxies at compile time. A native AOT build runs in CI.</p></div>
<div class="mx-feature"><div class="mx-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/></svg></div><h3>Plays well with .NET</h3><p>Runs behind Microsoft.Extensions.DependencyInjection, the Generic Host and ASP.NET Core, and passes Microsoft's own conformance suite.</p></div>
<div class="mx-feature"><div class="mx-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 18l5-6 4 3 7-9"/><path d="M4 21h16"/></svg></div><h3>Honest about speed</h3><p>A singleton resolves in about 20 ns. Microsoft's container is faster in places, and the comparison page says where.</p></div>
<div class="mx-feature"><div class="mx-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8 5l-5 7 5 7M16 5l5 7-5 7"/></svg></div><h3>A sample for every feature</h3><p>Runnable examples for lifetimes, decorators, profiles, the generator, the Generic Host, ASP.NET Core and OpenTelemetry.</p></div>
<div class="mx-feature"><div class="mx-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3l9 16H3z"/><path d="M12 10v4M12 17h.01"/></svg></div><h3>Every warning explained</h3><p>MAXEN001 to MAXEN009: what each one means and how to fix it.</p></div>
</div>
</section>
<section class="mx-section">
<div class="mx-compare-head"><h2>How it compares</h2><a href="docs/comparison.md">Read the full comparison</a></div>
<div class="mx-compare">
<table>
<thead><tr><th>Feature</th><th>Maxen.DI</th><th>Microsoft DI</th><th>Autofac</th></tr></thead>
<tbody>
<tr><td>Whole-graph validation without building anything</td><td>yes</td><td>partly</td><td>no</td></tr>
<tr><td>Diagnostics in your editor while you type</td><td>yes</td><td>no</td><td>no</td></tr>
<tr><td>Source-generated registrations and proxies</td><td>yes</td><td>no</td><td>no</td></tr>
<tr><td>Decorators</td><td>built in</td><td>add-on</td><td>built in</td></tr>
</tbody>
</table>
</div>
</section>
