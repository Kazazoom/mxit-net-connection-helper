# USING THE GIT REPOSITORY

## Getting Started

We use Git to manage versioning and contributions on the MxitConnectionHelper
project, via our GitHub repository. Below is our guide to using Git with our
project. We ask that you try and follow it where possible, to make collaboration
easy for everyone.

## Why Git?

For a thorough discussion on the pros and cons of Git compared to centralized
source code control systems, just Google "Git vs your_favorite_scm_here".
There are plenty of flame wars going on there. As developers, we prefer Git
above all other tools around today. Git really changed the way developers think
of merging and branching. From the classic CVS/Subversion world most developers
come from, merging/branching has always been considered a bit scary ("beware of
merge conflicts, they bite you!"ù) and something you only do every once in a
while.

But with Git, these actions are extremely cheap and simple, and they are
considered one of the core parts of your daily workflow, really. For example, in
CVS/Subversion books, branching and merging is first discussed in the later
chapters (for advanced users), while in every Git book, it"s already covered in
chapter 3 (basics).

As a consequence of its simplicity and repetitive nature, branching and merging
are no longer something to be afraid of. Version control tools are supposed to
assist in branching/merging more than anything else.

Enough about the tools, let"s head onto the development model. The model that we
are going to present here is essentially no more than a set of procedures that
every contributor has to follow in order to come to a managed code development
process.

## Setup your own public repository

Your first step is to establish a public repository on GitHub from which we can
pull your work into the main repository.

 1. Setup a GitHub account (https://github.com/), if you haven't already. In the
    examples below we are going to use `<username>`, you would replace this with
    your GitHub username.
 2. Fork the MxitConnectionHelper respository
    (https://github.com/Kazazoom/mxit-net-connection-helper/) by clicking the
    fork button on the top right of the page.
    This will create a public repository under your username:
    `https://github.com/<username>/mxit-net-connection-helper/`
 3. Clone your fork locally and enter it. (Remembering to replace `<username>`
    with your GitHub username)

    ```sh
    % git clone git@github.com:<username>/mxit-net-connection-helper.git
    % cd mxit-net-connection-helper
    ```

 5. Add a remote pointing to the main MxitConnectionHelper repository, so you
    can keep your fork up-to-date with the latest changes:

    ```sh
    % git remote add Kazazoom \
      https://github.com/Kazazoom/mxit-net-connection-helper.git
    % git fetch Kazazoom
    ```

## Branching model

### Main branch

At its core, the branching model is based off of a main branch with an infinite
lifetime:

 -  `master`

The `master` branch on the main repository (which we setup in step 5 above),
`Kazazoom/master`, is considered to be the main branch where the source code
always reflects a *production-ready* state.

### Supporting branches

Next to the main branch `Kazazoom/master`, we use different supporting branches
to aid parallel development between contributors, ease tracking of features,
prepare for production releases and to assist in quickly fixing problems
identified in live production releases. Unlike the main branch, these branches
always have a limited life time, since they will be removed eventually.

The two different types of supporting branches you may use are:

 -  `feature/*` branches
 -  `hotfix/*` branches

Each of these branches have a specific purpose and are bound to strict rules as
to which branches may be their originating branches and which branches they will
be merged into.

By no means are these branches "special" from a technical perspective, they are
just categorised by how we *use* them.

#### Feature branches

 -  Must branch off of `Kazazoom/master`
 -  Pull request target must be `Kazazoom/master`
 -  Branch naming convention: `feature/*`, where * must be a short but
    descriptive name of the feature. If you are working on something based off
    of the issue tracker, be it a bug fix, improvement or new feature, you must
    prepend the issue number to the feature name, e.g.:
    `feature/123-verbose-logging`

Feature branches (or sometimes called topic branches) are used to develop new
features or fix functionality for an upcoming or distant future release. When
starting development of a feature, the target release in which this feature
branch will be incorporated may well be unknown at that point. The essense of a
feature branch is that is exists as long as the feature is in development, but
will eventually be merged back into `Kazazoom/master` (to definitely add the
new feature to the upcoming release) or discarded (in the case of a
disappointing experiment).

Feature branches typically exist in contributors repos only, never in
`Kazazoom`.

#### Hotfix branches

 -  Must branch off of the tagged commit on `Kazazoom/master` that is affected
 -  Pull request target must be `Kazazoom/master`
 -  Branch naming convention: `hotfix/*`, where * is the version number of the
    release your are hotfixing, e.g.: `hotfix/1.2.3`

Hotfix branches arise from the necessity to act immediately upon an undesired
state of a live production version. When a critical bug in a production version
must be resolved immediately, a hotfix branch may be branched off from the
corresponding tag on the master branch that marks the production version.

Hotfixes are only for *critical* issues that need to be resolved immediately,
all other bugs must be fixed through the feature branch system.

## Keeping Up-to-Date

Periodically, you should update your fork to match the MxitConnectionHelper
repository. In the above setups, we have added a remote pointing to the main
repository, which allows you to do the following:

```sh
% git fetch Kazazoom
```

## Working on MxitConnectionHelper

When working on MxitConnectionHelper, we recommend you follow the branching
structure set out above.

A typical work flow will then consist of the following:

 1. Create a new local branch based off the main repository.
 2. Switch to your new local branch.
 3. Do some work, commit, repeat as necessary.
 4. Push the local branch to your remote repository on GitHub.
 5. Send a GitHub pull request.

The mechanics of this process are actually quite trivial. As an example, we will
create a branch for developing a new feature suggested in the issue tracker. The
issue ID 54 requests we add in a Unicorn Gene Sequencer into the next release.

```sh
% git fetch Kazazoom
% git checkout -b feature/42-unicorn-gene-sequencer Kazazoom/master
Switched to a new branch 'feature/42-unicorn-gene-sequencer'
```
... do some work ...
  
```sh
% git commit
```
... write your log message ...
... do some more work ...
  
```sh
% git commit
```
... write your log message ...

... feature is ready for integration, push it to your public repository ...
(make sure to read the section below on long running branches)
  
```sh
% git push origin feature/42-unicorn-gene-sequencer
Counting objects: 38, done.
Delta compression using up to 2 threads.
Compression objects: 100% (18/18), done.
Writing objects: 100% (20/20), 8.19KiB, done.
Total 20 (delta 12), reused 0 (delta 0)
To ssh://git@github.com/<username>/mxit-net-connection-helper.git
...
```

To get your contribution included in the project, you need to send a pull
request on GitHub.

Navigate to your public repository on GitHub
(`https://github.com/<username>/mxit-net-connection-helper/`), use the branch
dropdown to select the branch you just pushed, and then select the "Pull
Request" button in the upper right of the page.

Select `Kazazoom/mxit-new-advert-helper` as the _base repo_ and select `master`
as the _base branch_.

Make sure to include a meaningful title and description in your pull request,
and hit the green "Send pull request" button.

Your pull request will be reviewed, and once approved, merged into the main
repository.

## Long running branches

If you have been working on your feature branch for a decent amount of time, it
is very likely that other work will have been pulled onto the main repository
branch. Before you push your branch to your public repository, we recommend you
merge in any changes before you push yours.

```sh
% git fetch Kazazoom
% git merge Kazazoom/master
```

A shortcut to doing this is to do the following:

```sh
% git pull Kazazoom master
```

It is exactly the same as running a `git fetch` and then `git merge`.

If no new changes have been sent to the main repository, you should just see
`Already up-to-date.`. It is also possible you may have some merge conflicts,
which you will need to resolve before pushing your branch to the public
repository.

## Branch cleanup

As you might imagine, if you are a frequent contributor, you'll start to get a
ton of branches both locally and on your public repository.

Once you know that your pull request has been *accepted* into the main
repository, we suggest doing some cleanup of these branches.

 -  Local branch cleanup
    
    ```sh
    % git branch -d <branchname>
    ```

 -  Remote branch removal

    ```sh
    % git push origin :<branchname>
    ```
