// Copyright (c) 2011-2017 Silicon Studio Corp. All rights reserved. (https://www.siliconstudio.co.jp)
// See LICENSE.md for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SiliconStudio.Core.Annotations;
using SiliconStudio.Core.Extensions;
using SiliconStudio.Presentation.Commands;
using SiliconStudio.Presentation.Quantum.Presenters;
using SiliconStudio.Presentation.ViewModel;

namespace SiliconStudio.Presentation.Quantum.ViewModels
{
    /// <summary>
    /// A class that wraps an instance of <see cref="INodePresenterCommand"/> into an <see cref="ICommandBase"/> instance.
    /// </summary>
    public class NodePresenterCommandWrapper : CommandBase
    {
        [NotNull] private readonly IReadOnlyCollection<INodePresenter> presenters;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodePresenterCommandWrapper"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider of the view model.</param>
        /// <param name="presenters">The <see cref="INodePresenter"/> instances on which to invoke the command.</param>
        /// <param name="command">The command to invoke.</param>
        public NodePresenterCommandWrapper([NotNull] IViewModelServiceProvider serviceProvider, [NotNull] IReadOnlyCollection<INodePresenter> presenters, [NotNull] INodePresenterCommand command)
            : base(serviceProvider)
        {
            this.presenters = presenters ?? throw new ArgumentNullException(nameof(presenters));
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }

        /// <summary>
        /// The name of the action executed by this command.
        /// </summary>
        [NotNull]
        public string ActionName => $"Execute {Name}";

        /// <summary>
        /// The name of this command.
        /// </summary>
        [NotNull]
        public string Name => Command.Name;

        /// <summary>
        /// The command wrapped by this instance.
        /// </summary>
        [NotNull]
        public INodePresenterCommand Command { get; }

        /// <inheritdoc/>
        public override void Execute(object parameter)
        {
            Invoke(parameter).Forget();
        }

        /// <inheritdoc/>
        public override bool CanExecute(object parameter)
        {
            return Command.CanExecute(presenters, parameter);
        }

        /// <summary>
        /// Invokes the command on each node presenters attached to this wrapper.
        /// </summary>
        /// <param name="parameter">The parameter of the command.</param>
        /// <returns>A task that completes when the execution of the command is finished.</returns>
        public virtual async Task Invoke(object parameter)
        {
            var preExecuteResult = await Command.PreExecute(presenters, parameter);
            foreach (var presenter in presenters)
            {
                await Command.Execute(presenter, parameter, preExecuteResult);
            }
            await Command.PostExecute(presenters.ToList(), parameter);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return !string.IsNullOrEmpty(Name) ? Name : base.ToString();
        }
    }
}